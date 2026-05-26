"""HTTP client for the remote ingest API exposed by mcp_server.py (SSE / HTTP mode).

Mirrors the .NET ``RagTools.Ingest --remote`` flag pattern.

Design notes:
- Pure stdlib: urllib.request / urllib.error + zipfile — no extra dependency.
- Builds a single ZIP containing rag-config.yaml + metadata-rules.yaml + queries.yaml
  at the ZIP root, plus every file to ingest at its repo-relative path.
- POSTs the ZIP once to  POST /ingest/{collection}/batch  (Content-Type application/zip).
- Polls GET /ingest/{collection}/operations/{opId} until every op is Completed or Failed.
- Retries the batch POST on HTTP 503 (queue full) up to 3 times with exponential back-off.
- Returns 0 on full success, 1 if any file failed.

History: prior version POSTed each file to  POST /ingest/{collection}  (per-file JSON).
That route was removed when the batch ZIP endpoint became canonical, so the per-file
version returned HTTP 406 for every file on the current servers.
"""
from __future__ import annotations

import io
import json
import os
import time
import urllib.error
import urllib.request
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from common import Config


def _resolve_companion_paths(cfg: "Config") -> tuple["Path | None", "Path | None"]:
    """Re-resolve metadata-rules.yaml and queries.yaml paths the same way load_config did."""
    from common import _find_companion_file  # type: ignore[attr-defined]

    if env_meta := os.environ.get("RAG_METADATA"):
        meta_path: "Path | None" = Path(env_meta)
    elif cfg.config_path is not None:
        meta_path = _find_companion_file(
            cfg.config_path, "metadata_rules", "metadata-rules.yaml", raw=cfg.raw)
    else:
        meta_path = None

    if env_q := os.environ.get("RAG_QUERIES"):
        q_path: "Path | None" = Path(env_q)
    elif cfg.config_path is not None:
        q_path = _find_companion_file(
            cfg.config_path, "queries", "queries.yaml", raw=cfg.raw)
    else:
        q_path = None

    return meta_path, q_path


def _build_batch_zip(cfg: "Config", files: "list[Path]") -> bytes:
    """Build the batch ZIP payload expected by /ingest/{coll}/batch.

    Layout (all at ZIP root or under their workspace-relative path):
        rag-config.yaml
        metadata-rules.yaml        (referenced by config_files.metadata_rules)
        queries.yaml               (referenced by config_files.queries)
        <rel_path>                 for every file in `files`
    """
    if cfg.config_path is None:
        raise RuntimeError("cfg.config_path is required for remote batch upload")

    meta_path, q_path = _resolve_companion_paths(cfg)
    if meta_path is None or not meta_path.exists():
        raise RuntimeError(
            "metadata-rules.yaml not found — required for batch upload "
            "(set RAG_METADATA or add config_files.metadata_rules to rag-config.yaml)"
        )
    if q_path is None or not q_path.exists():
        raise RuntimeError(
            "queries.yaml not found — required for batch upload "
            "(set RAG_QUERIES or add config_files.queries to rag-config.yaml)"
        )

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("rag-config.yaml", cfg.config_path.read_bytes())
        z.writestr("metadata-rules.yaml", meta_path.read_bytes())
        z.writestr("queries.yaml", q_path.read_bytes())
        gloss = getattr(cfg, "glossary_path", None)
        if gloss is not None and Path(gloss).exists():
            z.writestr("multilingual-glossary.yaml", Path(gloss).read_bytes())
        for path in files:
            try:
                rel = path.relative_to(cfg.workspace).as_posix()
            except ValueError:
                rel = path.name
            try:
                z.writestr(rel, path.read_bytes())
            except OSError as exc:
                print(f"[ingest] SKIP {rel}: cannot read — {exc}")
    return buf.getvalue()


# Server queue capacity is 100 (ingest_worker.DEFAULT_CAPACITY); stay safely below.
BATCH_CHUNK_SIZE = 80


def push_files_to_remote_server(
    cfg: "Config",
    files: "list[Path]",
    base_url: str,
    api_key: "str | None" = None,
) -> int:
    """POST batch ZIP(s) of files to a running mcp_server ingest endpoint.

    Splits ``files`` into chunks of ``BATCH_CHUNK_SIZE`` so each batch fits under
    the server's queue capacity. Returns 0 on full success, 1 on any failure.
    """
    if not files:
        print("[ingest] remote push: no files to send")
        return 0

    total = len(files)
    if total <= BATCH_CHUNK_SIZE:
        return _push_one_batch(cfg, files, base_url, api_key, label=f"1/1 ({total})")

    chunks = [files[i:i + BATCH_CHUNK_SIZE] for i in range(0, total, BATCH_CHUNK_SIZE)]
    print(f"[ingest] splitting {total} file(s) into {len(chunks)} batch(es) of ≤{BATCH_CHUNK_SIZE}")
    overall = 0
    for idx, chunk in enumerate(chunks, 1):
        rc = _push_one_batch(
            cfg, chunk, base_url, api_key,
            label=f"{idx}/{len(chunks)} ({len(chunk)})",
        )
        if rc != 0:
            overall = rc
    return overall


def _push_one_batch(
    cfg: "Config",
    files: "list[Path]",
    base_url: str,
    api_key: "str | None",
    label: str,
) -> int:
    base = base_url.rstrip("/")
    collection = cfg.collection
    key = (api_key or os.environ.get("RAG_API_KEY", "")).strip()

    print(f"[ingest] building batch {label} ZIP → {collection}")
    try:
        zip_bytes = _build_batch_zip(cfg, files)
    except Exception as exc:  # noqa: BLE001
        print(f"[ingest] ERROR building batch ZIP: {exc}")
        return 1
    print(
        f"[ingest] batch ZIP ready ({len(zip_bytes)} bytes), "
        f"POST → {base}/ingest/{collection}/batch"
    )

    headers: dict[str, str] = {"Content-Type": "application/zip"}
    if key:
        headers["X-Api-Key"] = key

    batch_response: dict = {}
    max_attempts = 8  # ~1+2+4+8+16+30+30+30 = ~2 min of 503 retries (queue drain)
    for attempt in range(max_attempts):
        req = urllib.request.Request(
            f"{base}/ingest/{collection}/batch",
            data=zip_bytes,
            headers=headers,
            method="POST",
        )
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                batch_response = json.loads(resp.read())
            break
        except urllib.error.HTTPError as exc:
            body = ""
            try:
                body = exc.read().decode("utf-8", "replace")[:300]
            except Exception:
                pass
            if exc.code == 503 and attempt < max_attempts - 1:
                wait = min(30, 2 ** attempt)
                print(f"[ingest] batch POST: queue full (503), retry in {wait}s … {body}")
                time.sleep(wait)
                continue
            print(f"[ingest] ERROR batch POST failed — HTTP {exc.code}: {body}")
            return 1
        except Exception as exc:  # noqa: BLE001
            print(f"[ingest] ERROR batch POST failed — {exc}")
            return 1
    else:
        return 1

    operations = batch_response.get("operations") or []
    if not operations:
        print(f"[ingest] WARNING server returned no operations: {batch_response!r}")
        return 1

    op_ids = [op["operation_id"] for op in operations if op.get("operation_id")]
    rel_by_id = {op["operation_id"]: op.get("rel_path", "?") for op in operations}
    print(f"[ingest] batch accepted — {len(op_ids)} operation(s), polling …")

    poll_headers: dict[str, str] = {}
    if key:
        poll_headers["X-Api-Key"] = key

    succeeded = failed = 0
    deadline = time.time() + max(180.0, len(op_ids) * 5.0)
    remaining = set(op_ids)
    last_status: dict[str, str] = {}

    while remaining and time.time() < deadline:
        time.sleep(2)
        for op_id in list(remaining):
            poll_req = urllib.request.Request(
                f"{base}/ingest/{collection}/operations/{op_id}",
                headers=poll_headers,
            )
            try:
                with urllib.request.urlopen(poll_req, timeout=15) as resp:
                    op = json.loads(resp.read())
            except urllib.error.HTTPError as exc:
                if exc.code == 404:
                    last_status[op_id] = "NotFound"
                    print(f"[ingest] FAIL {rel_by_id[op_id]}: operation 404")
                    failed += 1
                    remaining.discard(op_id)
                continue
            except Exception:
                continue
            status = (op.get("status") or "").strip()
            last_status[op_id] = status
            if status.lower() in ("completed", "complete"):
                chunks = op.get("chunk_count") or op.get("chunkCount") or "?"
                print(f"[ingest] OK  {rel_by_id[op_id]} → {chunks} chunk(s)")
                succeeded += 1
                remaining.discard(op_id)
            elif status.lower() == "failed":
                err = op.get("error_message") or op.get("errorMessage") or "unknown error"
                print(f"[ingest] FAIL {rel_by_id[op_id]}: {err}")
                failed += 1
                remaining.discard(op_id)

    if remaining:
        for op_id in remaining:
            print(
                f"[ingest] TIMEOUT {rel_by_id[op_id]}: still {last_status.get(op_id, 'Queued')!r}"
            )
            failed += 1

    print(
        f"[ingest] remote push complete: {succeeded} ok, {failed} failed, 0 skipped "
        f"({len(files)} total)"
    )
    return 0 if failed == 0 else 1
