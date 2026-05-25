"""HTTP client for the remote ingest API exposed by mcp_server.py (SSE / HTTP mode).

Mirrors the .NET ``RagTools.Ingest --remote`` flag pattern.

Usage (from ingest.py --remote):
    from remote_ingest_client import push_files_to_remote_server

    rc = push_files_to_remote_server(cfg, files_to_process, args.remote, args.api_key)

Design notes:
- Pure stdlib: urllib.request / urllib.error — no extra dependency.
- POST each file to  POST /ingest/{collection}  with JSON body.
- Retries on HTTP 503 (queue full) up to 3 times with exponential back-off.
- Polls the operation-status URL returned in the response until Completed / Failed.
- Returns 0 on full success, 1 if any file failed.
"""
from __future__ import annotations

import json
import os
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from common import Config


def push_files_to_remote_server(
    cfg: "Config",
    files: "list[Path]",
    base_url: str,
    api_key: "str | None" = None,
) -> int:
    """POST each file to a running mcp_server ingest endpoint.

    Parameters
    ----------
    cfg:
        Resolved RAG config (used for ``cfg.collection``, ``cfg.workspace``,
        and ``detect_doc_kind``).
    files:
        Absolute paths to the markdown files that need (re-)ingesting.
    base_url:
        Root URL of the running mcp_server, e.g. ``http://localhost:3002``.
        The trailing slash is optional.
    api_key:
        Optional ``X-Api-Key`` header value.  Falls back to the
        ``RAG_API_KEY`` environment variable when *None* or empty.

    Returns
    -------
    int
        ``0`` if every file succeeded; ``1`` if at least one failed.
    """
    from common import detect_doc_kind

    base = base_url.rstrip("/")
    collection = cfg.collection
    key = (api_key or os.environ.get("RAG_API_KEY", "")).strip()
    headers: dict[str, str] = {"Content-Type": "application/json"}
    if key:
        headers["X-Api-Key"] = key

    succeeded = failed = skipped = 0

    for path in files:
        rel_path = path.relative_to(cfg.workspace).as_posix()

        try:
            content = path.read_text(encoding="utf-8")
        except OSError as exc:
            print(f"[ingest] SKIP {rel_path}: cannot read — {exc}")
            skipped += 1
            continue

        doc_kind = detect_doc_kind(rel_path, cfg)
        body = json.dumps({"relPath": rel_path, "content": content, "docKind": doc_kind}).encode()

        # POST with retry on 503 (server queue full).
        location: str | None = None
        for attempt in range(3):
            req = urllib.request.Request(
                f"{base}/ingest/{collection}",
                data=body,
                headers=headers,
                method="POST",
            )
            try:
                with urllib.request.urlopen(req, timeout=30) as resp:
                    result = json.loads(resp.read())
                    location = result.get("location")
                break
            except urllib.error.HTTPError as exc:
                if exc.code == 503 and attempt < 2:
                    wait = 2 ** attempt
                    print(f"[ingest] {rel_path}: queue full (503), retry in {wait}s …")
                    time.sleep(wait)
                else:
                    print(f"[ingest] ERROR {rel_path}: POST failed — HTTP {exc.code}")
                    failed += 1
                    location = None
                    break
            except Exception as exc:  # noqa: BLE001
                print(f"[ingest] ERROR {rel_path}: POST failed — {exc}")
                failed += 1
                location = None
                break

        if location is None:
            continue

        # Poll until Completed or Failed (hard deadline: 120 s).
        poll_url = f"{base}{location}"

        def _poll_req() -> urllib.request.Request:
            return urllib.request.Request(poll_url, headers=headers)

        status = "Queued"
        op: dict = {}
        deadline = time.time() + 120
        while time.time() < deadline:
            time.sleep(2)
            try:
                with urllib.request.urlopen(_poll_req(), timeout=10) as resp:
                    op = json.loads(resp.read())
                    status = op.get("status", "")
                    if status in ("Completed", "Failed"):
                        break
            except Exception:  # noqa: BLE001
                pass  # transient network glitch — keep polling

        if status == "Completed":
            chunks = op.get("chunkCount", "?")
            print(f"[ingest] OK  {rel_path} → {chunks} chunk(s)")
            succeeded += 1
        elif status == "Failed":
            err = op.get("errorMessage", "unknown error")
            print(f"[ingest] FAIL {rel_path}: {err}")
            failed += 1
        else:
            print(f"[ingest] TIMEOUT {rel_path}: still {status!r} after 120s")
            failed += 1

    print(
        f"[ingest] remote push complete: {succeeded} ok, {failed} failed, {skipped} skipped "
        f"({len(files)} total)"
    )
    return 0 if failed == 0 else 1
