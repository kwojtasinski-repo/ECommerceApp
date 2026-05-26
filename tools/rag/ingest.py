"""Build the Qdrant index from docs/.

Usage:
    python tools/rag/ingest.py                  # incremental mode (default) — re-embeds only changed files
    python tools/rag/ingest.py --mode docker    # uses running Qdrant at vector_store.url
    python tools/rag/ingest.py --force-full     # recreate collection from scratch
    python tools/rag/ingest.py --dry-run        # parse + chunk only, print stats, no embeddings

Incremental mode: compares sha256 hashes of each file against tools/rag/.cache/manifest.json.
Only changed/new files are re-embedded. Chunks for deleted/changed files are removed from Qdrant.
The manifest is always written at the end, even when nothing changed.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import sys
import time
from pathlib import Path

from tqdm import tqdm

from chunker import chunk_markdown
from common import (
    REPO_ROOT,
    CONFIG_PATH,
    Config,
    detect_adr_id,
    detect_doc_kind,
    iter_markdown_files,
    load_config,
    resolve_weight,
    sanitize_text,
)


def _file_doc_title(rel_path: str, raw_text: str) -> str:
    """Use the first H1 if present, otherwise fall back to the relative path."""
    for line in raw_text.splitlines():
        s = line.strip()
        if s.startswith("# "):
            return s[2:].strip()
        if s and not s.startswith("#") and not s.startswith("---"):
            break
    return rel_path


def _build_payload(rel_path: str, doc_title: str, chunk, weight: float, cfg=None) -> dict:
    return {
        "rel_path": rel_path,
        "doc_title": doc_title,
        "doc_kind": detect_doc_kind(rel_path, cfg),
        "adr_id": detect_adr_id(rel_path, cfg),
        "breadcrumb": chunk.breadcrumb,
        "heading_path": chunk.heading_path,
        "start_line": chunk.start_line,
        "end_line": chunk.end_line,
        "token_count": chunk.token_count,
        "weight": weight,
        "text": chunk.text,
    }


def _make_qdrant_client(
    cfg: "Config",
    mode: str,
    dim: int,
    incremental: bool,
    files_to_delete: "list[str]",
):
    """Return a configured QdrantClient for the requested mode.

    Handles collection creation / recreation and stale-chunk deletion so that
    ``main()`` does not need to branch on mode three times.
    """
    from qdrant_client import QdrantClient
    from qdrant_client.models import (
        Distance, FieldCondition, Filter, FilterSelector, MatchValue, VectorParams
    )

    if mode == "memory":
        client = QdrantClient(":memory:")
        client.recreate_collection(
            collection_name=cfg.collection,
            vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
        )
        return client

    if mode == "local":
        client = QdrantClient(path=cfg.vector_local_path)
    else:  # docker
        client = QdrantClient(url=cfg.vector_url)

    if not incremental:
        client.recreate_collection(
            collection_name=cfg.collection,
            vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
        )
    else:
        existing = {c.name for c in client.get_collections().collections}
        if cfg.collection not in existing:
            client.create_collection(
                collection_name=cfg.collection,
                vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
            )
        if files_to_delete:
            for rel_path in files_to_delete:
                client.delete(
                    collection_name=cfg.collection,
                    points_selector=FilterSelector(
                        filter=Filter(must=[
                            FieldCondition(key="rel_path", match=MatchValue(value=rel_path))
                        ])
                    ),
                )
            print(f"[ingest] deleted stale chunks for {len(files_to_delete)} file(s)")

    return client


def _stable_id(rel_path: str, breadcrumb: str, start_line: int) -> int:
    h = hashlib.blake2b(f"{rel_path}|{breadcrumb}|{start_line}".encode(), digest_size=8)
    return int.from_bytes(h.digest(), "big")


def _sha256_file(path: Path) -> str:
    """Return hex SHA-256 of the file's raw bytes."""
    h = hashlib.sha256()
    h.update(path.read_bytes())
    return h.hexdigest()


def _load_file_manifest(cfg: Config) -> dict:
    """Return stored file-hash manifest, or empty structure if none exists."""
    p = cfg.manifest_path
    if p.exists():
        with p.open("r", encoding="utf-8") as fh:
            data = json.load(fh)
            if "file_hashes" in data:
                return data
    return {"last_indexed": None, "file_hashes": {}}


def _save_file_manifest(
    cfg: Config,
    file_hashes: dict[str, str],
    total_files: int,
    total_chunks: int,
    dim: int,
) -> None:
    """Persist the per-file hash manifest so the next run can skip unchanged files."""
    cfg.manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest = {
        "last_indexed": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "mode": cfg.vector_mode,
        "files": total_files,
        "chunks": total_chunks,
        "model": cfg.embedder_model,
        "dim": dim,
        "file_hashes": file_hashes,
    }
    with cfg.manifest_path.open("w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)


def _write_stats_md(
    cfg: Config,
    points: list,
    total_files: int,
) -> None:
    """Write docs/rag/index-stats.md — a human-readable index fingerprint.

    The file is committed to the repo so reviewers can audit coverage without
    running queries or starting Docker. It is NOT a replacement for the MCP tools.

    Layout:
      - Summary header: timestamp, collection, total files/chunks.
      - Breakdown table: doc_kind → file count + chunk count.
      - Per-file detail table: rel_path → doc_kind + chunk count (sorted).
    """
    stats_path = cfg.stats_path
    if stats_path is None:
        return  # stats_path not configured — skip silently

    # Aggregate chunk counts and file counts per doc_kind.
    kind_chunks: dict[str, int] = {}
    kind_files: dict[str, set[str]] = {}
    file_chunks: dict[str, int] = {}
    file_kind: dict[str, str] = {}

    for point in points:
        payload = point.payload if hasattr(point, "payload") else point.get("payload", {})
        rel = payload.get("rel_path", "")
        kind = payload.get("doc_kind", "unknown")
        kind_chunks[kind] = kind_chunks.get(kind, 0) + 1
        kind_files.setdefault(kind, set()).add(rel)
        file_chunks[rel] = file_chunks.get(rel, 0) + 1
        file_kind[rel] = kind

    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    total_chunks = sum(kind_chunks.values())

    lines: list[str] = [
        "# RAG Index Stats",
        "",
        f"Last indexed: {now}  ",
        f"Collection: `{cfg.collection}`  ",
        f"Files: {total_files}  ",
        f"Chunks: {total_chunks}  ",
        f"Model: `{cfg.embedder_model}`  ",
        "",
        "## Breakdown by doc_kind",
        "",
        "| doc_kind | files | chunks |",
        "|----------|------:|-------:|",
    ]
    for kind in sorted(kind_chunks):
        lines.append(f"| `{kind}` | {len(kind_files[kind])} | {kind_chunks[kind]} |")

    lines += [
        "",
        "## Per-file detail",
        "",
        "| file | doc_kind | chunks |",
        "|------|----------|-------:|",
    ]
    for rel in sorted(file_chunks):
        lines.append(f"| `{rel}` | `{file_kind[rel]}` | {file_chunks[rel]} |")

    lines.append("")

    stats_path.parent.mkdir(parents=True, exist_ok=True)
    stats_path.write_text("\n".join(lines), encoding="utf-8")


# ── Remote push mode (--remote URL) ───────────────────────────────────────────

def _remote_push(
    cfg: "Config",
    files: "list[Path]",
    base_url: str,
    api_key: "str | None",
) -> int:
    """Delegate to the dedicated RemoteIngestClient module."""
    from remote_ingest_client import push_files_to_remote_server
    return push_files_to_remote_server(cfg, files, base_url, api_key)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", default=None, metavar="PATH",
                        help="Path to rag-config.yaml (default: rag-config.yaml next to ingest.py)")
    parser.add_argument("--mode", choices=["memory", "docker", "local"], default=None,
                        help="Override vector_store.mode from rag-config.yaml")
    parser.add_argument("--dry-run", action="store_true",
                        help="Parse + chunk only, print stats, no embeddings or upserts")
    parser.add_argument("--force-full", action="store_true",
                        help="Recreate collection from scratch (ignores manifest cache)")
    parser.add_argument(
        "--remote", default=None, metavar="URL",
        help=(
            "Base URL of a running mcp_server.py with the HTTP Streamable transport "
            "(e.g. http://localhost:3002). When set, files are POSTed to "
            "POST /ingest/{collection} instead of being embedded locally."
        ),
    )
    parser.add_argument(
        "--api-key", default=None, metavar="KEY",
        help="X-Api-Key header value sent with every remote request (overrides RAG_API_KEY env var).",
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true",
        help="Enable verbose (DEBUG-level) logging output.",
    )
    args = parser.parse_args()

    if args.verbose:
        import logging
        logging.basicConfig(level=logging.DEBUG, format="[%(levelname)s] %(name)s: %(message)s")

    if args.config:
        config_path = Path(args.config).resolve()
    elif env_cfg := os.environ.get("RAG_CONFIG"):
        # Docker per-file-mount mode: baked config path supplied explicitly.
        # Takes priority over RAG_WORKSPACE so the selective-mount compose setup works.
        config_path = Path(env_cfg)
    elif env_ws := os.environ.get("RAG_WORKSPACE"):
        # Container / CI mode: derive config from workspace env — no --config needed.
        config_path = Path(env_ws) / "tools" / "rag" / "rag-config.yaml"
    else:
        config_path = CONFIG_PATH
    cfg: Config = load_config(config_path)
    mode = args.mode or cfg.vector_mode

    all_files = iter_markdown_files(cfg)
    print(f"[ingest] found {len(all_files)} markdown files under {[r.name for r in cfg.source_roots]}")

    # -- incremental change detection --
    stored = _load_file_manifest(cfg)
    stored_hashes: dict[str, str] = stored.get("file_hashes", {})
    current_hashes: dict[str, str] = {
        path.relative_to(cfg.workspace).as_posix(): _sha256_file(path)
        for path in all_files
    }

    if args.force_full or not stored_hashes:
        files_to_process = all_files
        files_to_delete: list[str] = []
        incremental = False
        label = "full rebuild" if (args.force_full or stored_hashes) else "first run"
        print(f"[ingest] {label} — indexing all {len(all_files)} files")
    else:
        changed_rels = {
            rel for rel, h in current_hashes.items()
            if stored_hashes.get(rel) != h
        }
        new_rels = {rel for rel in current_hashes if rel not in stored_hashes}
        deleted_rels = {rel for rel in stored_hashes if rel not in current_hashes}
        process_rels = changed_rels | new_rels
        files_to_process = [
            p for p in all_files
            if p.relative_to(cfg.workspace).as_posix() in process_rels
        ]
        files_to_delete = list(deleted_rels | changed_rels)
        incremental = True
        print(
            f"[ingest] incremental: {len(process_rels)} changed/new, "
            f"{len(deleted_rels)} deleted, {len(all_files) - len(process_rels)} unchanged"
        )
        if not process_rels and not deleted_rels:
            print("[ingest] nothing changed — index is up to date")
            _save_file_manifest(cfg, current_hashes, len(all_files),
                                stored.get("chunks", 0), stored.get("dim", 0))
            return 0

    # ── Remote push: skip local embedding, POST to running mcp_server ────────
    if args.remote:
        print(f"[ingest] remote mode → {args.remote}/ingest/{cfg.collection} ({len(files_to_process)} file(s))")
        rc = _remote_push(cfg, files_to_process, args.remote, args.api_key)
        # Save manifest on the client side so subsequent remote runs are incremental.
        # Chunk count is unknown (server-side stat) — store 0 as a sentinel.
        _save_file_manifest(cfg, current_hashes, len(all_files), 0, 0)
        return rc

    # -- chunk only the files that need processing --
    chunks_by_file: list[tuple[Path, list]] = []
    total_chunks = 0
    for path in files_to_process:
        rel = path.relative_to(cfg.workspace).as_posix()
        text = sanitize_text(path.read_text(encoding="utf-8-sig", errors="replace"), rel)
        doc_title = _file_doc_title(rel, text)
        chunks = chunk_markdown(text, doc_title, cfg.chunker)
        chunks_by_file.append((path, chunks))
        total_chunks += len(chunks)
    print(f"[ingest] will embed {total_chunks} chunks across {len(files_to_process)} file(s)")

    if args.dry_run:
        # Print kind distribution for sanity-checking the chunker.
        kind_counts: dict[str, int] = {}
        for path, chunks in chunks_by_file:
            rel = path.relative_to(cfg.workspace).as_posix()
            kind = detect_doc_kind(rel, cfg)
            kind_counts[kind] = kind_counts.get(kind, 0) + len(chunks)
        for k, v in sorted(kind_counts.items(), key=lambda kv: -kv[1]):
            print(f"  {k:25s} {v:5d} chunks")
        return 0

    # Heavy imports are deferred so --dry-run stays fast.
    from qdrant_client.models import PointStruct
    from make_embedder import make_embedder

    print(f"[ingest] loading embedder: {cfg.embedder_model} (device={cfg.embedder_device})")
    embedder = make_embedder(cfg)
    dim = embedder.dimensions
    print(f"[ingest] embedding dimension: {dim}")

    client = _make_qdrant_client(cfg, mode, dim, incremental, files_to_delete)

    points: list[PointStruct] = []
    print("[ingest] embedding...")
    started = time.time()
    for path, chunks in tqdm(chunks_by_file, desc="files"):
        if not chunks:
            continue
        rel = path.relative_to(cfg.workspace).as_posix()
        doc_title = _file_doc_title(rel, sanitize_text(path.read_text(encoding="utf-8-sig", errors="replace"), rel))
        weight = resolve_weight(rel, path.stat().st_size, cfg.ranking)
        embed_texts = [c.embed_text for c in chunks]
        vectors = embedder.embed_batch(embed_texts)
        for chunk, vec in zip(chunks, vectors):
            payload = _build_payload(rel, doc_title, chunk, weight, cfg)
            points.append(
                PointStruct(
                    id=_stable_id(rel, chunk.breadcrumb, chunk.start_line),
                    vector=vec,
                    payload=payload,
                )
            )

    print(f"[ingest] upserting {len(points)} points...")
    # Upsert in batches to keep memory usage flat for larger corpora.
    BATCH = 256
    for i in range(0, len(points), BATCH):
        client.upsert(collection_name=cfg.collection, points=points[i : i + BATCH])
    print(f"[ingest] done in {time.time() - started:.1f}s")

    if mode == "memory":
        # Persist a JSON snapshot so query.py can reload without re-embedding.
        cfg.snapshot_path.parent.mkdir(parents=True, exist_ok=True)
        snapshot = {
            "collection": cfg.collection,
            "dim": dim,
            "model": cfg.embedder_model,
            "points": [
                {"id": p.id, "vector": p.vector, "payload": p.payload}
                for p in points
            ],
        }
        with cfg.snapshot_path.open("w", encoding="utf-8") as fh:
            json.dump(snapshot, fh)
        print(f"[ingest] snapshot written: {cfg.snapshot_path.relative_to(cfg.workspace)}")

    # Always save the hash manifest (both memory and docker modes).
    _write_stats_md(cfg, points, len(all_files))
    if cfg.stats_path is not None:
        print(f"[ingest] stats written:    {cfg.stats_path.relative_to(cfg.workspace)}")
    _save_file_manifest(cfg, current_hashes, len(all_files), len(points), dim)
    print(f"[ingest] manifest written: {cfg.manifest_path.relative_to(cfg.workspace)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
