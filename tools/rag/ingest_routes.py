"""Starlette route handlers for the HTTP ingest API.

Mirrors the .NET IngestController endpoints (ADR-0028 tech-details-dotnet.md):

    POST  /ingest/{collection}/batch                         → 202 | 400 | 503
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
import json
import sys
import zipfile
from typing import Any

from starlette.requests import Request
from starlette.responses import JSONResponse, Response
from starlette.routing import Route

from ingest_worker import IngestJob
from operation_store import OperationStore


def build_ingest_routes(
    store: OperationStore,
    queue: asyncio.Queue,
    capacity: int = 100,
) -> list[Route]:
    """Return Starlette Route objects wired to the given store and queue."""

    # ── POST /ingest/{collection}/batch ────────────────────────────────────────

    async def upload_batch(request: Request) -> Response:
        collection = request.path_params["collection"]

        body_bytes = await request.body()
        try:
            zf = zipfile.ZipFile(io.BytesIO(body_bytes))
        except zipfile.BadZipFile:
            return JSONResponse({"error": "Invalid ZIP archive"}, status_code=400)

        # ── Validate required config files ───────────────────────────────────
        import yaml as _yaml
        zip_names = {info.filename for info in zf.infolist()}
        missing_configs = [n for n in ("metadata-rules.yaml", "queries.yaml") if n not in zip_names]
        if missing_configs:
            return JSONResponse(
                {"error": f"ZIP must contain: {', '.join(missing_configs)}"},
                status_code=400,
            )
        try:
            meta_raw = _yaml.safe_load(
                zf.read("metadata-rules.yaml").decode("utf-8", errors="replace")
            ) or {}
        except Exception:
            return JSONResponse(
                {"error": "metadata-rules.yaml is not valid YAML"}, status_code=400
            )
        doc_kind_rules = meta_raw.get("doc_kind_rules") or []
        if not doc_kind_rules:
            return JSONResponse(
                {"error": "metadata-rules.yaml must contain at least one doc_kind_rules entry"},
                status_code=400,
            )
        try:
            queries_raw = _yaml.safe_load(
                zf.read("queries.yaml").decode("utf-8", errors="replace")
            ) or {}
        except Exception:
            return JSONResponse(
                {"error": "queries.yaml is not valid YAML"}, status_code=400
            )
        named_queries = queries_raw.get("named_queries") or []
        if not named_queries:
            return JSONResponse(
                {"error": "queries.yaml must contain at least one named_queries entry"},
                status_code=400,
            )
        known_kinds = {r.get("kind") for r in doc_kind_rules if r.get("kind")}
        bad_kinds = sorted(
            q.get("doc_kind")
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

        _CONFIG_FILES = {"metadata-rules.yaml", "queries.yaml"}
        file_entries = [
            info for info in zf.infolist()
            if not info.is_dir() and info.filename not in _CONFIG_FILES
        ]
        if not file_entries:
            return JSONResponse({"error": "ZIP contains no files"}, status_code=400)

        if queue.qsize() + len(file_entries) > capacity:
            return JSONResponse(
                {"error": "Service Unavailable — ingest queue is full, retry later"},
                status_code=503,
            )

        operations_list = []
        for info in file_entries:
            rel_path = info.filename
            content = zf.read(info.filename).decode("utf-8", errors="replace")

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

        import uuid as _uuid

        batch_id = f"batch:{collection}:{_uuid.uuid4()}"
        return JSONResponse(
            {"batchId": batch_id, "count": len(operations_list), "operations": operations_list},
            status_code=202,
        )

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
