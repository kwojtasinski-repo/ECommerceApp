"""Build the Qdrant index from docs/.

Usage:
    python tools/rag/ingest.py                  # in-memory mode, dumps snapshot to .cache/
    python tools/rag/ingest.py --mode docker    # uses running Qdrant at vector_store.url
    python tools/rag/ingest.py --dry-run        # parse + chunk only, print stats, no embeddings

The script is idempotent: it always recreates the collection from scratch.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
from pathlib import Path

from tqdm import tqdm

from chunker import chunk_markdown
from common import (
    REPO_ROOT,
    Config,
    detect_adr_id,
    detect_doc_kind,
    iter_markdown_files,
    load_config,
    resolve_weight,
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


def _build_payload(rel_path: str, doc_title: str, chunk, weight: float) -> dict:
    return {
        "rel_path": rel_path,
        "doc_title": doc_title,
        "doc_kind": detect_doc_kind(rel_path),
        "adr_id": detect_adr_id(rel_path),
        "breadcrumb": chunk.breadcrumb,
        "heading_path": chunk.heading_path,
        "start_line": chunk.start_line,
        "end_line": chunk.end_line,
        "token_count": chunk.token_count,
        "weight": weight,
        "text": chunk.text,
    }


def _stable_id(rel_path: str, breadcrumb: str, start_line: int) -> int:
    h = hashlib.blake2b(f"{rel_path}|{breadcrumb}|{start_line}".encode(), digest_size=8)
    return int.from_bytes(h.digest(), "big")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["memory", "docker"], default=None,
                        help="Override vector_store.mode from config.yaml")
    parser.add_argument("--dry-run", action="store_true",
                        help="Parse + chunk only, print stats, no embeddings or upserts")
    args = parser.parse_args()

    cfg: Config = load_config()
    mode = args.mode or cfg.vector_mode

    files = iter_markdown_files(cfg)
    print(f"[ingest] found {len(files)} markdown files under {[r.name for r in cfg.source_roots]}")

    chunks_by_file: list[tuple[Path, list]] = []
    total_chunks = 0
    for path in files:
        text = path.read_text(encoding="utf-8")
        doc_title = _file_doc_title(path.relative_to(REPO_ROOT).as_posix(), text)
        chunks = chunk_markdown(text, doc_title, cfg.chunker)
        chunks_by_file.append((path, chunks))
        total_chunks += len(chunks)
    print(f"[ingest] produced {total_chunks} chunks across {len(files)} files")

    if args.dry_run:
        # Print kind distribution for sanity-checking the chunker.
        kind_counts: dict[str, int] = {}
        for path, chunks in chunks_by_file:
            rel = path.relative_to(REPO_ROOT).as_posix()
            kind = detect_doc_kind(rel)
            kind_counts[kind] = kind_counts.get(kind, 0) + len(chunks)
        for k, v in sorted(kind_counts.items(), key=lambda kv: -kv[1]):
            print(f"  {k:25s} {v:5d} chunks")
        return 0

    # Heavy imports are deferred so --dry-run stays fast.
    from sentence_transformers import SentenceTransformer
    from qdrant_client import QdrantClient
    from qdrant_client.models import Distance, PointStruct, VectorParams

    print(f"[ingest] loading embedder: {cfg.embedder_model} (device={cfg.embedder_device})")
    model = SentenceTransformer(cfg.embedder_model, device=cfg.embedder_device)
    dim = model.get_sentence_embedding_dimension()
    print(f"[ingest] embedding dimension: {dim}")

    if mode == "memory":
        client = QdrantClient(":memory:")
    else:
        client = QdrantClient(url=cfg.vector_url)

    client.recreate_collection(
        collection_name=cfg.collection,
        vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
    )

    points: list[PointStruct] = []
    print("[ingest] embedding...")
    started = time.time()
    for path, chunks in tqdm(chunks_by_file, desc="files"):
        if not chunks:
            continue
        rel = path.relative_to(REPO_ROOT).as_posix()
        doc_title = _file_doc_title(rel, path.read_text(encoding="utf-8"))
        weight = resolve_weight(rel, path.stat().st_size, cfg.ranking)
        embed_texts = [c.embed_text for c in chunks]
        vectors = model.encode(
            embed_texts,
            batch_size=cfg.raw["embedder"].get("batch_size", 32),
            show_progress_bar=False,
            normalize_embeddings=True,
        )
        for chunk, vec in zip(chunks, vectors):
            payload = _build_payload(rel, doc_title, chunk, weight)
            points.append(
                PointStruct(
                    id=_stable_id(rel, chunk.breadcrumb, chunk.start_line),
                    vector=vec.tolist(),
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
        manifest = {
            "files": len(files),
            "chunks": len(points),
            "model": cfg.embedder_model,
            "dim": dim,
            "snapshot_path": str(cfg.snapshot_path.relative_to(REPO_ROOT)),
        }
        with cfg.manifest_path.open("w", encoding="utf-8") as fh:
            json.dump(manifest, fh, indent=2)
        print(f"[ingest] snapshot written: {cfg.snapshot_path.relative_to(REPO_ROOT)}")
        print(f"[ingest] manifest written: {cfg.manifest_path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
