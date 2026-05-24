"""Starlette route handlers for the HTTP ingest API.

Mirrors the .NET IngestController endpoints (ADR-0028 tech-details-dotnet.md):

    POST  /ingest/{collection}/batch                         → 202 | 400 | 413 | 415 | 503
    GET   /ingest/{collection}/operations/{operation_id}     → 200 | 404
    GET   /ingest/{collection}/operations                    → 200
    GET   /admin/stats                                       → 200

Usage (in mcp_server.py):
    from ingest_routes import build_ingest_routes
    routes = build_ingest_routes(store, queue, capacity)
    app = Starlette(routes=[*existing_routes, *routes])
"""
from __future__ import annotations

import asyncio
import io
import re
import uuid
import zipfile
from typing import Any

import yaml

from starlette.requests import Request
from starlette.responses import JSONResponse, Response
from starlette.routing import Route

from ingest_worker import IngestJob
from operation_store import OperationStore

_MAX_BODY_BYTES  = 50 * 1024 * 1024                          # 50 MB hard limit
_COLLECTION_RE   = re.compile(r'^[a-z0-9][a-z0-9_-]*$')     # safe collection names
_CONFIG_FILES    = frozenset({"metadata-rules.yaml", "queries.yaml"})
_OPTIONAL_CONFIG = frozenset({"multilingual-glossary.yaml"}) # processed but not required


def build_ingest_routes(
    store: OperationStore,
    queue: asyncio.Queue,
    capacity: int = 100,
) -> list[Route]:
    """Return Starlette Route objects wired to the given store and queue."""

    # ── POST /ingest/{collection}/batch ────────────────────────────────────────

    async def upload_batch(request: Request) -> Response:
        collection = request.path_params["collection"]

        # ── Collection name sanitization ─────────────────────────────────────
        if not _COLLECTION_RE.match(collection):
            return JSONResponse(
                {"error": f"Invalid collection name '{collection}'. Must match [a-z0-9][a-z0-9_-]*."},
                status_code=400,
            )

        # ── Content-Type validation ──────────────────────────────────────────
        ct = request.headers.get("content-type", "")
        if not ct.startswith(("application/zip", "application/octet-stream")):
            return JSONResponse(
                {"error": f"Expected Content-Type application/zip, got '{ct}'"},
                status_code=415,
            )

        # ── Body size limit ──────────────────────────────────────────────────
        body_bytes = await request.body()
        if len(body_bytes) == 0:
            return JSONResponse({"error": "Request body is empty"}, status_code=400)
        if len(body_bytes) > _MAX_BODY_BYTES:
            mb = _MAX_BODY_BYTES // (1024 * 1024)
            return JSONResponse(
                {"error": f"Request body too large ({len(body_bytes):,} bytes). Limit is {mb} MB."},
                status_code=413,
            )

        # ── Parse ZIP ────────────────────────────────────────────────────────
        try:
            zf = zipfile.ZipFile(io.BytesIO(body_bytes))
        except zipfile.BadZipFile:
            return JSONResponse({"error": "Invalid ZIP archive"}, status_code=400)

        with zf:
            all_entries = zf.infolist()
            zip_names   = {info.filename for info in all_entries}
            yaml_names  = sorted(n for n in zip_names if n.endswith((".yaml", ".yml")))

            # ── Required config files ────────────────────────────────────────
            for required in ("metadata-rules.yaml", "queries.yaml"):
                if required not in zip_names:
                    hint = (f" Found YAML files: {yaml_names}" if yaml_names else "")
                    return JSONResponse(
                        {"error": f"Required file '{required}' not found in ZIP root.{hint}"},
                        status_code=400,
                    )

            try:
                meta_raw = yaml.safe_load(
                    zf.read("metadata-rules.yaml").decode("utf-8", errors="replace")
                ) or {}
            except Exception as exc:
                return JSONResponse(
                    {"error": f"metadata-rules.yaml is not valid YAML: {exc}"}, status_code=400
                )
            doc_kind_rules = meta_raw.get("doc_kind_rules") or []
            if not doc_kind_rules:
                return JSONResponse(
                    {"error": "metadata-rules.yaml must contain at least one doc_kind_rules entry"},
                    status_code=400,
                )

            try:
                queries_raw = yaml.safe_load(
                    zf.read("queries.yaml").decode("utf-8", errors="replace")
                ) or {}
            except Exception as exc:
                return JSONResponse(
                    {"error": f"queries.yaml is not valid YAML: {exc}"}, status_code=400
                )
            named_queries = queries_raw.get("named_queries") or []
            if not named_queries:
                return JSONResponse(
                    {"error": "queries.yaml must contain at least one named_queries entry"},
                    status_code=400,
                )

            known_kinds = {r.get("kind") for r in doc_kind_rules if r.get("kind")}
            bad_kinds = sorted(
                q["doc_kind"]
                for q in named_queries
                if q.get("doc_kind") and q["doc_kind"] not in known_kinds
            )
            if bad_kinds:
                return JSONResponse(
                    {
                        "error": (
                            f"queries.yaml references unknown doc_kind(s): {bad_kinds}. "
                            "Add matching rules to metadata-rules.yaml."
                        )
                    },
                    status_code=400,
                )

            # ── Warnings accumulator ─────────────────────────────────────────
            warnings: list[str] = []
            if "multilingual-glossary.yaml" not in zip_names:
                warnings.append(
                    "multilingual-glossary.yaml not found in ZIP — "
                    "Polish/German query expansion will be reduced. "
                    "Include it for multilingual support."
                )

            # ── Document entries (non-config, non-dir, .md only, non-zero) ───
            _all_config = _CONFIG_FILES | _OPTIONAL_CONFIG
            file_entries = []
            for info in all_entries:
                if info.is_dir():
                    continue
                if info.filename in _all_config:
                    continue
                # Path traversal protection
                safe = info.filename.replace("\\", "/")
                if ".." in safe.split("/"):
                    return JSONResponse(
                        {"error": f"Path traversal detected in ZIP entry '{info.filename}'"},
                        status_code=400,
                    )
                # Extension check — only .md files accepted as documents
                if not info.filename.lower().endswith(".md"):
                    warnings.append(f"Skipped non-.md file: '{info.filename}'")
                    continue
                # Zero-byte filter
                if info.file_size == 0:
                    warnings.append(f"Skipped zero-byte file: '{info.filename}'")
                    continue
                file_entries.append(info)

            if not file_entries:
                return JSONResponse({"error": "ZIP contains no .md document files"}, status_code=400)

            if queue.qsize() + len(file_entries) > capacity:
                return JSONResponse(
                    {"error": "Service Unavailable — ingest queue is full, retry later"},
                    status_code=503,
                )

            operations_list = []
            for info in file_entries:
                rel_path = info.filename.replace("\\", "/")
                content  = zf.read(info.filename).decode("utf-8", errors="replace")

                op = await store.enqueue(collection, rel_path)
                job = IngestJob(
                    operation_id=op.operation_id,
                    collection=collection,
                    rel_path=rel_path,
                    content=content,
                    doc_kind=None,
                )
                await queue.put(job)
                status_url = f"/ingest/{collection}/operations/{op.operation_id}"
                operations_list.append(
                    {"relPath": rel_path, "operationId": op.operation_id, "statusUrl": status_url}
                )

        batch_id = f"batch:{collection}:{uuid.uuid4()}"
        response: dict[str, Any] = {
            "batchId": batch_id,
            "count": len(operations_list),
            "operations": operations_list,
        }
        if warnings:
            response["warnings"] = warnings
        return JSONResponse(response, status_code=202)

    # ── GET /ingest/{collection}/operations/{operation_id} ─────────────────

    async def get_operation(request: Request) -> Response:
        collection = request.path_params["collection"]
        operation_id = request.path_params["operation_id"]

        op = await store.get(operation_id)
        if op is None or op.collection != collection:
            return JSONResponse({"error": "Operation not found"}, status_code=404)

        return JSONResponse(op.to_dict())

    # ── GET /ingest/{collection}/operations ────────────────────────────────

    async def list_operations(request: Request) -> Response:
        collection = request.path_params["collection"]
        ops = await store.list_for_collection(collection)
        return JSONResponse(
            {"operations": [op.to_dict() for op in ops], "count": len(ops)}
        )

    # ── GET /admin/stats ───────────────────────────────────────────────────

    async def admin_stats(_request: Request) -> Response:
        return JSONResponse(
            {
                "queue_depth": store.queue_depth(),
                "retention_hours": 1,
                "total_operations": store.total_count(),
            }
        )

    return [
        Route("/ingest/{collection}/batch", endpoint=upload_batch, methods=["POST"]),
        Route(
            "/ingest/{collection}/operations",
            endpoint=list_operations,
            methods=["GET"],
        ),
        Route(
            "/ingest/{collection}/operations/{operation_id}",
            endpoint=get_operation,
            methods=["GET"],
        ),
        Route("/admin/stats", endpoint=admin_stats, methods=["GET"]),
    ]
