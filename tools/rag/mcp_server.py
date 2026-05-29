"""MCP server exposing 4 tools backed by the RAG index.

Tools:
- query_docs(question, bc=None, top_k=5)  -> ranked chunks: rel_path + breadcrumb + score + text snippet
- read_docs(question, bc=None, top_files=3) -> relevant chunks grouped by file (default) OR full file content
                                              when query signals full-content intent ("show me all details", etc.)
- get_history(id)                         -> all indexed chunks for a history group, sorted by start_line
                                            (Qdrant-based; uses collection-configured history field, default adr_id)
- list_adrs()                             -> table of all ADRs (id, title, kind counts)

Typical agent flow:
  1. list_adrs()           — orientation: what exists?
  2. query_docs(question)  — discovery: which files are relevant?
  3. read_docs(question)   — depth: best chunks per file (or full file when "show me all details about...")
  4. get_history(id)       — evolution: how did a specific document group change over time?

Run (VS Code Copilot starts this automatically via .vscode/mcp.json):
    python tools/rag/mcp_server.py [--config /path/to/rag-config.yaml]

--config is optional. Default: rag-config.yaml next to mcp_server.py.
Pass it when the config lives somewhere non-standard, e.g. for local dev
pointing at a project that doesn't use the standard tools/rag/ layout.
"""
from __future__ import annotations

import argparse
import asyncio
import contextlib
import logging
import os
import re
import sys
from pathlib import Path

from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import TextContent, Tool

import state
from common import CONFIG_PATH, load_config
from ingest_worker import DEFAULT_CAPACITY
from query import QueryEngine
from rag_tools import (
    _tool_get_history,
    _tool_list_adrs,
    _tool_query_docs,
    _tool_query_docs_cached,
    _tool_read_docs,
)

# ── MCP server instance ───────────────────────────────────────────────────────

SERVER = Server("ecommerceapp-rag")


# ── Tool schemas ──────────────────────────────────────────────────────────────

@SERVER.list_tools()
async def list_tools() -> list[Tool]:
    return [
        Tool(
            name="query_docs",
            description=(
                "Semantic search across project documentation (ADRs, architecture, patterns, "
                "reference, roadmap). Returns the top-k retrieved chunks with breadcrumb, "
                "file path, line range, weighted score, and text snippet. "
                "Use this for orientation — to discover which files are relevant. "
                "Follow up with read_docs to get full file content. "
                "Use bc to substring-filter by bounded context name (e.g. 'Sales/Orders')."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "bc": {"type": "string", "description": "Optional substring filter on breadcrumb / doc title"},
                    "top_k": {"type": "integer", "default": 5, "minimum": 1, "maximum": 15},
                },
                "required": ["question"],
            },
        ),
        Tool(
            name="read_docs",
            description=(
                "Semantic search that returns the best matching chunks grouped by file. "
                "When the question contains explicit full-content intent phrases "
                "(e.g. 'show me all details about', 'full content of', 'explain everything about', "
                "'entire', 'whole file', 'in full', 'complete picture') the server instead reads "
                "the whole file from disk and returns the complete text. "
                "Use this over query_docs when you need to reason over document context, not "
                "just a single fragment. Use bc to substring-filter by bounded context."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "bc": {"type": "string", "description": "Optional substring filter on breadcrumb / doc title"},
                    "top_files": {"type": "integer", "default": 3, "minimum": 1, "maximum": 5,
                                  "description": "Max unique files to return (each may be large)"},
                },
                "required": ["question"],
            },
        ),
        Tool(
            name="get_history",
            description=(
                "Return all indexed chunks for a document group identified by a history ID "
                "(e.g. ADR number, RFC number). Chunks are returned in chronological order "
                "(sorted by start_line). The grouping field is collection-defined "
                "(defaults to 'adr_id')."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "id": {"type": "string", "description": "History ID (e.g. '0016', 'RFC-003')"},
                },
                "required": ["id"],
            },
        ),
        Tool(
            name="list_adrs",
            description=(
                "List all ADRs in the repository with id, title, and presence of amendments / "
                "example-implementation files. Use for orientation queries like 'what ADRs exist?'."
            ),
            inputSchema={"type": "object", "properties": {}},
        ),
        Tool(
            name="query_docs_cached",
            description=(
                "Phase 7 L2 — RAG search packaged for caching into context-mode's FTS5 store. "
                "Runs the same semantic search as query_docs/read_docs (chunk mode, grouped by file), "
                "formats the top results as a markdown document with breadcrumbs preserved, and "
                "returns it together with a deterministic source label of the form "
                "'rag-cache-adr<NNNN>-<hash8>', 'rag-cache-<bc>-<hash8>', or 'rag-cache-q-<hash8>'. "
                "The caller (agent) then makes one follow-up ctx_index(content=<markdown>, source=<source>) call "
                "to persist the cache, and subsequent recalls use ctx_search(queries=[...], source='rag-cache-...'). "
                "Use this when the same scope will be re-read 3+ times in the session — direct query_docs is cheaper "
                "for one-shot questions. Does not write to context-mode itself (no inter-MCP coupling)."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "bc": {"type": "string", "description": "Optional substring filter on breadcrumb / doc title"},
                    "top_files": {"type": "integer", "default": 3, "minimum": 1, "maximum": 5,
                                  "description": "Max unique files to include in the cached markdown"},
                },
                "required": ["question"],
            },
        ),
    ]


# ── Tool dispatch ─────────────────────────────────────────────────────────────

_TOOL_DISPATCH = {
    "query_docs":         _tool_query_docs,
    "query_docs_cached":  _tool_query_docs_cached,
    "read_docs":          _tool_read_docs,
    "get_history":        _tool_get_history,
    "list_adrs":          _tool_list_adrs,
}

_log = logging.getLogger("ecommerceapp-rag.mcp")
_PATH_RE = re.compile(r"['\"]?(?:[A-Za-z]:)?[\\/](?:[^\s'\":()]+[\\/])+([^\s'\":()]+)['\"]?")


def _sanitize_error_message(exc: BaseException) -> str:
    """Return a user-safe error message: strip absolute filesystem paths and cap length."""
    msg = str(exc) or type(exc).__name__
    msg = _PATH_RE.sub(r"<path>/\1", msg)
    return msg[:500]


def _install_exception_handlers(app) -> None:  # noqa: ANN001
    """Register Starlette global exception handlers so /ingest and other HTTP routes
    return a sanitized JSON envelope instead of the default HTML 500 page.

    Mirrors the .NET ApiExceptionHandler — same payload shape, same sanitisation.
    HTTPException is passed through to Starlette so explicit 4xx responses
    (e.g. raised by ingest_routes) keep their status code and detail.
    """
    from starlette.exceptions import HTTPException
    from starlette.responses import JSONResponse

    async def _on_exception(_request, exc: Exception):  # noqa: ANN001
        _log.exception("unhandled HTTP exception: %s", type(exc).__name__)
        payload = {"error": _sanitize_error_message(exc), "code": "InternalServerError"}
        return JSONResponse(payload, status_code=500)

    async def _on_http_exception(_request, exc: HTTPException):  # noqa: ANN001
        payload = {"error": _sanitize_error_message(exc), "code": "HttpError"}
        return JSONResponse(payload, status_code=exc.status_code)

    app.add_exception_handler(HTTPException, _on_http_exception)
    app.add_exception_handler(Exception, _on_exception)


@SERVER.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    handler = _TOOL_DISPATCH.get(name)
    if handler is None:
        # MCP SDK converts raised exceptions to CallToolResult(isError=true).
        raise ValueError(f"Unknown tool: {name}")
    try:
        return await handler(arguments)
    except Exception as exc:
        _log.exception("tool '%s' failed", name)
        safe = _sanitize_error_message(exc)
        raise RuntimeError(f"{type(exc).__name__}: {safe}") from None


# ── Session context manager ───────────────────────────────────────────────────


@contextlib.contextmanager
def _bind_session_project(project: "str | None"):
    """Bind the per-request/session collection from the ?project= query param.

    Wraps the ``state._session_collection`` ContextVar token lifecycle so the
    HTTP Streamable handler and the legacy SSE handler share the same pattern
    without duplication.
    """
    token = state._session_collection.set(project)
    try:
        yield
    finally:
        state._session_collection.reset(token)


# ── Ingest component factory ──────────────────────────────────────────────────


def _make_ingest_components() -> "tuple[OperationStore, asyncio.Queue, IngestWorker, Any | None]":
    """Build the shared ingest queue, worker, operation store, and DocumentStore.

    Called once per transport startup (HTTP Streamable or legacy SSE).  Extracted to avoid
    duplicating the same lines in both ``_run_sse`` and ``_run_http``.

    The returned DocumentStore is ``None`` when the active vector store is not Qdrant
    (memory mode, missing client, embedder dimension not yet resolved) — the ingest
    controller treats that as "no per-collection config persistence" and behaves as before.
    """
    from ingest_worker import IngestWorker, _build_process_fn
    from operation_store import OperationStore

    store = OperationStore()
    queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)
    process_fn = _build_process_fn(state.ENGINE, state.CFG, store)
    worker = IngestWorker(queue, process_fn)

    document_store: "Any | None" = None
    try:
        from storage.document_store import DocumentStore
        state.ENGINE._ensure()
        client = state.ENGINE._client
        dims = state.ENGINE.embedder.dimensions
        if client is not None and dims:
            document_store = DocumentStore(client, dims)
    except Exception as exc:
        print(
            f"[rag-mcp] WARN: per-collection config persistence disabled ({type(exc).__name__}: {exc})",
            file=sys.stderr,
        )

    return store, queue, worker, document_store


# ── Transport: stdio ──────────────────────────────────────────────────────────


async def _run_stdio() -> None:
    async with stdio_server() as (read, write):
        await SERVER.run(read, write, SERVER.create_initialization_options())


# ── Transport: SSE ────────────────────────────────────────────────────────────


async def _run_sse(port: int) -> None:
    """Run the MCP server over SSE (HTTP).  VS Code connects via mcp.json type:sse."""
    from mcp.server.sse import SseServerTransport
    from starlette.applications import Starlette
    from starlette.routing import Mount, Route
    import uvicorn

    sse = SseServerTransport("/messages/")

    async def handle_sse(request):  # noqa: ANN001
        with _bind_session_project(request.query_params.get("project")):
            async with sse.connect_sse(
                request.scope, request.receive, request._send
            ) as streams:
                await SERVER.run(streams[0], streams[1], SERVER.create_initialization_options())

    from api_key_middleware import ApiKeyMiddleware
    from ingest_routes import build_ingest_routes
    from config.bootstrap import build_config_source

    _store, _queue, _worker, _doc_store = _make_ingest_components()
    state.CONFIG_SOURCE = build_config_source(state.CFG, _doc_store)

    @contextlib.asynccontextmanager
    async def lifespan(_app):  # noqa: ANN001
        _worker.start()
        yield
        await _worker.stop()

    ingest_routes = build_ingest_routes(
        _store, _queue, DEFAULT_CAPACITY,
        document_store=_doc_store,
        config_source=state.CONFIG_SOURCE,
        mounted_cfg=state.CFG,
    )

    app = Starlette(
        lifespan=lifespan,
        routes=[
            Route("/sse", endpoint=handle_sse),
            Mount("/messages/", app=sse.handle_post_message),
            *ingest_routes,
        ],
    )
    app.add_middleware(ApiKeyMiddleware)
    _install_exception_handlers(app)
    config = uvicorn.Config(app, host="0.0.0.0", port=port, log_level="warning")
    server = uvicorn.Server(config)
    print(f"[rag-mcp] SSE endpoint:   http://0.0.0.0:{port}/sse", file=sys.stderr)
    print(f"[rag-mcp] ingest API:     http://0.0.0.0:{port}/ingest/{{collection}}", file=sys.stderr)
    api_key_set = bool(os.environ.get("RAG_API_KEY", "").strip())
    print(f"[rag-mcp] auth:           {'X-Api-Key required' if api_key_set else 'no auth (RAG_API_KEY not set)'}", file=sys.stderr)
    await server.serve()


# ── Transport: Streamable HTTP ────────────────────────────────────────────────


async def _run_http(port: int) -> None:
    """Run the MCP server over Streamable HTTP (POST /).

    VS Code connects via mcp.json type:http, url:http://host:PORT/?project=<collection>.
    Requires mcp>=1.9.4 (1.8.x had BrokenResourceError crashing the task group on client disconnect).
    """
    from mcp.server.streamable_http_manager import StreamableHTTPSessionManager
    from starlette.applications import Starlette
    from starlette.routing import Mount, Route
    import uvicorn

    session_manager = StreamableHTTPSessionManager(
        app=SERVER,
        event_store=None,
        json_response=False,
    )

    async def handle_mcp(scope, receive, send) -> None:  # noqa: ANN001
        query_string = scope.get("query_string", b"").decode()
        project: str | None = None
        for part in query_string.split("&"):
            if part.startswith("project="):
                project = part[len("project="):]
                break
        with _bind_session_project(project):
            await session_manager.handle_request(scope, receive, send)

    from api_key_middleware import ApiKeyMiddleware
    from ingest_routes import build_ingest_routes
    from config.bootstrap import build_config_source

    _store, _queue, _worker, _doc_store = _make_ingest_components()
    state.CONFIG_SOURCE = build_config_source(state.CFG, _doc_store)

    @contextlib.asynccontextmanager
    async def lifespan(_app):  # noqa: ANN001
        # Run the session manager in a background task that auto-restarts if a
        # client disconnect (BrokenResourceError) crashes the inner task group.
        # MCP 1.9.x+ should handle this gracefully, but the guard is kept as
        # belt-and-suspenders for any edge cases in HTTP/2 or proxied setups.
        async def _sm_guard() -> None:
            while True:
                try:
                    async with session_manager.run():
                        await asyncio.sleep(float("inf"))
                except asyncio.CancelledError:
                    return
                except Exception as _exc:
                    print(
                        f"[rag-mcp] session manager restarting after {type(_exc).__name__}: {_exc}",
                        file=sys.stderr,
                    )
                    await asyncio.sleep(0.05)

        sm_task = asyncio.ensure_future(_sm_guard())
        await asyncio.sleep(0.05)  # let the session manager initialize before serving
        _worker.start()
        try:
            yield
        finally:
            sm_task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await sm_task
            await _worker.stop()

    ingest_routes = build_ingest_routes(
        _store, _queue, DEFAULT_CAPACITY,
        document_store=_doc_store,
        config_source=state.CONFIG_SOURCE,
        mounted_cfg=state.CFG,
    )

    app = Starlette(
        lifespan=lifespan,
        routes=[
            *ingest_routes,
            Mount("/", app=handle_mcp),
        ],
    )
    app.add_middleware(ApiKeyMiddleware)
    _install_exception_handlers(app)
    config = uvicorn.Config(app, host="0.0.0.0", port=port, log_level="warning")
    server = uvicorn.Server(config)
    print(f"[rag-mcp] endpoint:   http://0.0.0.0:{port}/ (MCP Streamable HTTP)", file=sys.stderr)
    print(f"[rag-mcp] ingest API: http://0.0.0.0:{port}/ingest/{{collection}}", file=sys.stderr)
    api_key_set = bool(os.environ.get("RAG_API_KEY", "").strip())
    print(f"[rag-mcp] auth:       {'X-Api-Key required' if api_key_set else 'no auth (RAG_API_KEY not set)'}", file=sys.stderr)
    await server.serve()



# ── Entrypoint ────────────────────────────────────────────────────────────────


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="ECommerceApp RAG MCP server")
    parser.add_argument(
        "--config",
        default=None,
        metavar="PATH",
        help=(
            "Path to rag-config.yaml. Default: rag-config.yaml next to mcp_server.py. "
            "Pass when the config lives somewhere non-standard "
            "(e.g. local dev without Docker, or a multi-project setup)."
        ),
    )
    return parser.parse_args()


if __name__ == "__main__":
    _args = _parse_args()
    if _args.config:
        _config_path = Path(_args.config).resolve()
    elif env_cfg := os.environ.get("RAG_CONFIG"):
        _config_path = Path(env_cfg)
    elif env_ws := os.environ.get("RAG_WORKSPACE"):
        _config_path = Path(env_ws) / "tools" / "rag" / "rag-config.yaml"
    else:
        _config_path = CONFIG_PATH

    state.CFG = load_config(_config_path)

    # ── Validate that every resolved file actually exists on disk ─────────────
    _errors: list[str] = []

    _meta_path = Path(os.environ["RAG_METADATA"]) if "RAG_METADATA" in os.environ else None
    if _meta_path is not None and not _meta_path.exists():
        _errors.append(f"RAG_METADATA={_meta_path} - file not found (check --volume mount)")

    _queries_path = Path(os.environ["RAG_QUERIES"]) if "RAG_QUERIES" in os.environ else None
    if _queries_path is not None and not _queries_path.exists():
        _errors.append(f"RAG_QUERIES={_queries_path} - file not found (check --volume mount)")

    _manifest_path = state.CFG.manifest_path
    if "RAG_MANIFEST" in os.environ and not _manifest_path.exists():
        _errors.append(
            f"RAG_MANIFEST={_manifest_path} - file not found "
            "(run ingest.py first, then mount the resulting .rag/manifest.json)"
        )

    if _errors:
        for _e in _errors:
            print(f"[rag-mcp] ERROR: {_e}", file=sys.stderr)
        sys.exit(1)

    state.ENGINE = QueryEngine(state.CFG)

    def _file_tag(path: "Path | None", fallback: str) -> str:
        if path is None:
            return fallback
        try:
            return f"{path} ({path.stat().st_size} bytes)"
        except OSError:
            return f"{path} (unreadable)"

    print(f"[rag-mcp] config:     {_config_path}", file=sys.stderr)
    print(f"[rag-mcp] workspace:  {state.CFG.workspace}", file=sys.stderr)
    print(f"[rag-mcp] collection: {state.CFG.collection} | mode: {state.CFG.vector_mode} | tool timeout: {state.TOOL_TIMEOUT:.0f}s", file=sys.stderr)
    print(f"[rag-mcp] metadata:   {_file_tag(_meta_path, '<companion metadata-rules.yaml>')}", file=sys.stderr)
    print(f"[rag-mcp] queries:    {_file_tag(_queries_path, '<companion queries.yaml>')}", file=sys.stderr)
    print(f"[rag-mcp] manifest:   {_file_tag(_manifest_path if 'RAG_MANIFEST' in os.environ else None, str(_manifest_path))}", file=sys.stderr)

    print("[rag-mcp] loading embedding model...", file=sys.stderr)
    try:
        state.ENGINE._ensure()
        print("[rag-mcp] embedding model ready", file=sys.stderr)
    except Exception as _exc:
        print(f"[rag-mcp] ERROR: model load failed: {_exc}", file=sys.stderr)
        sys.exit(1)

    _transport = os.environ.get("MCP_TRANSPORT", "stdio").lower()
    _port = int(os.environ.get("MCP_PORT", "3002"))
    _has_port = _transport in ("sse", "http")
    print(f"[rag-mcp] transport:  {_transport}{f' (port {_port})' if _has_port else ''}", file=sys.stderr)

    if _transport == "http":
        asyncio.run(_run_http(_port))
    elif _transport == "sse":
        asyncio.run(_run_sse(_port))
    else:
        asyncio.run(_run_stdio())
