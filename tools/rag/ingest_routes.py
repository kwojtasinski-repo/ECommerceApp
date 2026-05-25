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

Design:
- ``IngestController`` — one instance per server; holds store, queue, capacity.
- ``_parse_zip_batch``  — pure function; parses ZIP body, returns data or error response.
                          Keeps HTTP concerns out of the ZIP/YAML parsing logic.
- ``build_ingest_routes`` — factory; constructs IngestController and returns Route list.
"""
from __future__ import annotations

import asyncio
import fnmatch
import io
import re
import uuid
import zipfile
from dataclasses import dataclass, field
from typing import Any

import yaml

from starlette.requests import Request
from starlette.responses import JSONResponse, Response
from starlette.routing import Route

from ingest_worker import IngestJob
from operation_store import OperationStore

_MAX_BODY_BYTES = 50 * 1024 * 1024                           # 50 MB hard limit
_COLLECTION_RE  = re.compile(r'^[a-z0-9][a-z0-9_-]*$')      # safe collection names
_CONFIG_FILES   = frozenset({"metadata-rules.yaml", "queries.yaml"})
_OPTIONAL_CONFIG = frozenset({"multilingual-glossary.yaml"}) # processed but not required


# ── ZIP batch parsing (business logic, no HTTP objects) ───────────────────────


@dataclass
class _FileEntry:
    """A single document file extracted from the ingest ZIP."""
    rel_path: str
    content: str
    doc_kind: str
    adr_id: "str | None"


@dataclass
class _ZipBatchContent:
    """Successful result of parsing a ZIP ingest payload."""
    entries: list[_FileEntry]
    warnings: list[str] = field(default_factory=list)


def _parse_zip_batch(
    body_bytes: bytes,
    capacity: int,
    queue_size: int,
) -> "tuple[_ZipBatchContent, None] | tuple[None, Response]":
    """Parse a ZIP ingest payload and return document entries or an error Response.

    Separates ZIP/YAML business logic from HTTP request handling so that
    ``IngestController.upload_batch`` only deals with HTTP concerns.

    Returns ``(_ZipBatchContent, None)`` on success or ``(None, JSONResponse)`` on error.
    """
    try:
        zf = zipfile.ZipFile(io.BytesIO(body_bytes))
    except zipfile.BadZipFile:
        return None, JSONResponse({"error": "Invalid ZIP archive"}, status_code=400)

    with zf:
        all_entries = zf.infolist()
        zip_names   = {info.filename for info in all_entries}
        yaml_names  = sorted(n for n in zip_names if n.endswith((".yaml", ".yml")))

        # ── Required config files ──────────────────────────────────────────
        for required in ("metadata-rules.yaml", "queries.yaml"):
            if required not in zip_names:
                hint = f" Found YAML files: {yaml_names}" if yaml_names else ""
                return None, JSONResponse(
                    {"error": f"Required file '{required}' not found in ZIP root.{hint}"},
                    status_code=400,
                )

        # ── Parse metadata-rules.yaml ──────────────────────────────────────
        try:
            meta_raw = yaml.safe_load(
                zf.read("metadata-rules.yaml").decode("utf-8", errors="replace")
            ) or {}
        except Exception as exc:
            return None, JSONResponse(
                {"error": f"metadata-rules.yaml is not valid YAML: {exc}"}, status_code=400
            )

        doc_kind_rules = meta_raw.get("doc_kind_rules") or []
        if not doc_kind_rules:
            return None, JSONResponse(
                {"error": "metadata-rules.yaml must contain at least one doc_kind_rules entry"},
                status_code=400,
            )

        adr_id_patterns = [r["pattern"] for r in meta_raw.get("adr_id_patterns", []) if r.get("pattern")]

        def _detect_doc_kind(rel_path: str) -> str:
            p = rel_path.replace("\\", "/")
            for rule in doc_kind_rules:
                if fnmatch.fnmatch(p, rule.get("glob", "")):
                    return rule.get("kind", "other")
            return "other"

        def _detect_adr_id(rel_path: str) -> "str | None":
            p = rel_path.replace("\\", "/")
            for pattern in adr_id_patterns:
                m = re.search(pattern, p)
                if m:
                    try:
                        return m.group("id")
                    except IndexError:
                        return None
            return None

        # ── Parse queries.yaml ─────────────────────────────────────────────
        try:
            queries_raw = yaml.safe_load(
                zf.read("queries.yaml").decode("utf-8", errors="replace")
            ) or {}
        except Exception as exc:
            return None, JSONResponse(
                {"error": f"queries.yaml is not valid YAML: {exc}"}, status_code=400
            )

        named_queries = queries_raw.get("named_queries") or []
        if not named_queries:
            return None, JSONResponse(
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
            return None, JSONResponse(
                {
                    "error": (
                        f"queries.yaml references unknown doc_kind(s): {bad_kinds}. "
                        "Add matching rules to metadata-rules.yaml."
                    )
                },
                status_code=400,
            )

        # ── Optional config files / warnings ──────────────────────────────
        warnings: list[str] = []
        if "multilingual-glossary.yaml" not in zip_names:
            warnings.append(
                "multilingual-glossary.yaml not found in ZIP — "
                "Polish/German query expansion will be reduced. "
                "Include it for multilingual support."
            )

        # ── Document entries (non-config, non-dir, .md only, non-zero) ────
        _all_config = _CONFIG_FILES | _OPTIONAL_CONFIG
        entries: list[_FileEntry] = []
        for info in all_entries:
            if info.is_dir():
                continue
            if info.filename in _all_config:
                continue
            safe = info.filename.replace("\\", "/")
            if ".." in safe.split("/"):
                return None, JSONResponse(
                    {"error": f"Path traversal detected in ZIP entry '{info.filename}'"},
                    status_code=400,
                )
            if not info.filename.lower().endswith(".md"):
                warnings.append(f"Skipped non-.md file: '{info.filename}'")
                continue
            if info.file_size == 0:
                warnings.append(f"Skipped zero-byte file: '{info.filename}'")
                continue
            content = zf.read(info.filename).decode("utf-8", errors="replace")
            rel_path = info.filename.replace("\\", "/")
            entries.append(_FileEntry(
                rel_path=rel_path,
                content=content,
                doc_kind=_detect_doc_kind(rel_path),
                adr_id=_detect_adr_id(rel_path),
            ))

        if not entries:
            return None, JSONResponse({"error": "ZIP contains no .md document files"}, status_code=400)

        if queue_size + len(entries) > capacity:
            return None, JSONResponse(
                {"error": "Service Unavailable — ingest queue is full, retry later"},
                status_code=503,
            )

        return _ZipBatchContent(entries=entries, warnings=warnings), None


# ── IngestController ──────────────────────────────────────────────────────────


class IngestController:
    """HTTP controller for the ingest API.

    Holds shared infrastructure (store, queue, capacity) as instance attributes
    so route handlers are plain async methods instead of triple-nested closures.
    """

    def __init__(self, store: OperationStore, queue: asyncio.Queue, capacity: int) -> None:
        self._store    = store
        self._queue    = queue
        self._capacity = capacity

    # ── POST /ingest/{collection}/batch ───────────────────────────────────────

    async def upload_batch(self, request: Request) -> Response:
        collection = request.path_params["collection"]

        if not _COLLECTION_RE.match(collection):
            return JSONResponse(
                {"error": f"Invalid collection name '{collection}'. Must match [a-z0-9][a-z0-9_-]*."},
                status_code=400,
            )

        ct = request.headers.get("content-type", "")
        if not ct.startswith(("application/zip", "application/octet-stream")):
            return JSONResponse(
                {"error": f"Expected Content-Type application/zip, got '{ct}'"},
                status_code=415,
            )

        body_bytes = await request.body()
        if len(body_bytes) == 0:
            return JSONResponse({"error": "Request body is empty"}, status_code=400)
        if len(body_bytes) > _MAX_BODY_BYTES:
            mb = _MAX_BODY_BYTES // (1024 * 1024)
            return JSONResponse(
                {"error": f"Request body too large ({len(body_bytes):,} bytes). Limit is {mb} MB."},
                status_code=413,
            )

        batch_content, error_response = _parse_zip_batch(
            body_bytes,
            capacity=self._capacity,
            queue_size=self._queue.qsize(),
        )
        if error_response is not None:
            return error_response

        operations_list: list[dict[str, Any]] = []
        for entry in batch_content.entries:
            op = await self._store.enqueue(collection, entry.rel_path)
            job = IngestJob(
                operation_id=op.operation_id,
                collection=collection,
                rel_path=entry.rel_path,
                content=entry.content,
                doc_kind=entry.doc_kind,
                adr_id=entry.adr_id,
            )
            await self._queue.put(job)
            operations_list.append({
                "rel_path": entry.rel_path,
                "operation_id": op.operation_id,
                "status_url": f"/ingest/{collection}/operations/{op.operation_id}",
            })

        response: dict[str, Any] = {
            "batch_id": f"batch:{collection}:{uuid.uuid4()}",
            "count": len(operations_list),
            "operations": operations_list,
        }
        if batch_content.warnings:
            response["warnings"] = batch_content.warnings
        return JSONResponse(response, status_code=202)

    # ── GET /ingest/{collection}/operations/{operation_id} ────────────────────

    async def get_operation(self, request: Request) -> Response:
        collection   = request.path_params["collection"]
        operation_id = request.path_params["operation_id"]

        op = await self._store.get(operation_id)
        if op is None or op.collection != collection:
            return JSONResponse({"error": "Operation not found"}, status_code=404)

        return JSONResponse(op.to_dict())

    # ── GET /ingest/{collection}/operations ───────────────────────────────────

    async def list_operations(self, request: Request) -> Response:
        collection = request.path_params["collection"]
        ops = await self._store.list_for_collection(collection)
        return JSONResponse({"operations": [op.to_dict() for op in ops], "count": len(ops)})

    # ── GET /admin/stats ──────────────────────────────────────────────────────

    async def admin_stats(self, _request: Request) -> Response:
        return JSONResponse({
            "queue_depth": self._store.queue_depth(),
            "retention_hours": 1,
            "total_operations": self._store.total_count(),
        })


# ── Public factory ────────────────────────────────────────────────────────────


def build_ingest_routes(
    store: OperationStore,
    queue: asyncio.Queue,
    capacity: int = 100,
) -> list[Route]:
    """Return Starlette Route objects wired to the given store and queue."""
    ctrl = IngestController(store, queue, capacity)
    return [
        Route("/ingest/{collection}/batch",                          endpoint=ctrl.upload_batch,    methods=["POST"]),
        Route("/ingest/{collection}/operations",                     endpoint=ctrl.list_operations, methods=["GET"]),
        Route("/ingest/{collection}/operations/{operation_id}",      endpoint=ctrl.get_operation,   methods=["GET"]),
        Route("/admin/stats",                                        endpoint=ctrl.admin_stats,     methods=["GET"]),
    ]
