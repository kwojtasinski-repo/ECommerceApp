"""Starlette route handlers for the HTTP ingest API.

Mirrors the .NET IngestController endpoints (ADR-0028 tech-details-dotnet.md):

    POST  /ingest/{collection}                               → 202 | 400 | 503
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
import json
import sys

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

    # ── POST /ingest/{collection} ───────────────────────────────────────────

    async def upload(request: Request) -> Response:
        collection = request.path_params["collection"]

        try:
            body = await request.json()
        except Exception:
            return JSONResponse({"error": "Invalid JSON body"}, status_code=400)

        rel_path: str | None = body.get("relPath") or body.get("rel_path")
        content: str | None = body.get("content")

        if not rel_path:
            return JSONResponse({"error": "relPath is required"}, status_code=400)
        if not content:
            return JSONResponse({"error": "content is required"}, status_code=400)

        doc_kind: str | None = body.get("docKind") or body.get("doc_kind")

        if queue.full():
            return JSONResponse(
                {"error": "Service Unavailable — ingest queue is full, retry later"},
                status_code=503,
            )

        op = await store.enqueue(collection, rel_path)
        job = IngestJob(
            operation_id=op.operation_id,
            collection=collection,
            rel_path=rel_path,
            content=content,
            doc_kind=doc_kind,
        )
        await queue.put(job)

        location = f"/ingest/{collection}/operations/{op.operation_id}"
        return JSONResponse(
            {"operationId": op.operation_id, "status": "Queued", "location": location},
            status_code=202,
            headers={"Location": location},
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
        Route("/ingest/{collection}", endpoint=upload, methods=["POST"]),
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
