# ADR-0028 — Technical Details: Python MCP Server (Phase 2)

> **Router**: [ADR-0028 main decision](0028-remote-multitenant-rag-ingest.md) ·
> [.NET details](tech-details-dotnet.md) · **Python details** (this file) ·
> [Amendment 001](amendments/0028-001-implementation-deviations.md)

This companion document covers the Python implementation of the async ingest pipeline
added in Phase 2. It is the Python counterpart to `tech-details-dotnet.md`.
All architectural decisions live in the main ADR; this file records implementation
specifics.

---

## Component Overview

| Component | .NET equivalent | Python file |
|---|---|---|
| `IngestJob` | `IngestJob` record | `ingest_worker.py` |
| `IngestWorker` | `IngestWorker` (`BackgroundService`) | `ingest_worker.py` |
| `OperationStore` | `OperationStore` (`ConcurrentDictionary`) | `operation_store.py` |
| `IngestController` routes | ASP.NET Core `IngestController` | `ingest_routes.py` |
| `ApiKeyMiddleware` | ASP.NET Core `ApiKeyMiddleware` | `api_key_middleware.py` |
| `RagSession` | Scoped `RagSession` (HTTP / SSE DI) | `ContextVar[str | None]` in `state.py` |

---

## Operation Status Lifecycle

```
POST /ingest/{collection}
    └─► OperationStore.enqueue()  →  status: "Queued"
            └─► IngestJob placed in asyncio.Queue
                    └─► IngestWorker picks job
                            └─► mark_processing()  →  status: "Processing"
                                    ├─► chunk + embed + upsert → mark_completed(chunk_count) → "Completed"
                                    └─► exception             → mark_failed(error_message)   → "Failed"
```

---

## Operation Store — Retention Policy

Both Python and .NET use a **1-hour in-memory retention** policy:

| Implementation | Constant | Purge trigger |
|---|---|---|
| Python (`operation_store.py`) | `RETENTION_HOURS = 1` / `_RETENTION = timedelta(hours=1)` | On every `enqueue()` call |
| .NET (`OperationStore.cs`) | `RetentionPeriod = TimeSpan.FromHours(1)` | On every write operation |

> **Important**: operations expire 1 hour after `enqueued_at`. If the server restarts,
> all operations are lost (in-memory only). This is intentional for Phase 1 of both
> implementations. A future phase will persist to Qdrant (see ADR-0028 main, Section 3).

---

## Data Model

### `IngestOperation` (`operation_store.py`)

```python
@dataclass
class IngestOperation:
    operation_id: str          # UUID v4
    collection: str            # Qdrant collection name
    rel_path: str              # workspace-relative path of the document
    status: IngestStatus       # Queued | Processing | Completed | Failed
    enqueued_at: datetime      # UTC, set at creation
    started_at: datetime|None  # UTC, set when worker picks job
    completed_at: datetime|None  # UTC, set on Completed or Failed
    chunk_count: int           # set on Completed; 0 if no chunks produced
    error_message: str|None    # set on Failed
```

JSON wire format (snake_case — all keys are snake_case throughout the Python API):

```json
{
  "operation_id": "3fa85f64-...",
  "status": "Completed",
  "collection": "ecommerceapp-rag",
  "rel_path": "docs/adr/0001/0001-example.md",
  "enqueued_at": "2025-01-15T10:00:00+00:00",
  "started_at":  "2025-01-15T10:00:01+00:00",
  "completed_at":"2025-01-15T10:00:03+00:00",
  "manifest": { "indexed_chunks": 12, "doc_kind": "adr_main" },
  "error_message": null
}
```

---

## Async Queue + Worker

### `asyncio.Queue` (≈ .NET `Channel<IngestJob>`)

```python
_queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)  # default: 100
```

`queue.full()` → `503 Service Unavailable` from the route handler, same as .NET channel
`writer.TryWrite()` returning `false`.

### `IngestWorker` (≈ .NET `BackgroundService`)

Single `asyncio.Task` consumer started via `asyncio.create_task()` in the Starlette
lifespan context manager. The task loops forever calling `_queue.get()`, then calls
the process function:

```python
# ingest_worker.py — _build_process_fn closure
async def process(job: IngestJob) -> None:
    await store.mark_processing(job.operation_id)
    try:
        chunk_count = await asyncio.to_thread(_process_sync, job)  # CPU-bound
        await store.mark_completed(job.operation_id, chunk_count)
    except Exception as exc:
        await store.mark_failed(job.operation_id, str(exc))
```

`_process_sync(job)` runs in a thread pool via `asyncio.to_thread` because
`SentenceTransformer.encode()` is CPU-bound and would block the event loop.

### Idempotent Re-ingest

Before upserting new points, the worker deletes existing Qdrant points for the same
`rel_path` in the target collection:

```python
client.delete(collection_name=job.collection,
              points_selector=Filter(must=[
                  FieldCondition(key="rel_path", match=MatchValue(value=job.rel_path))
              ]))
```

This matches the .NET `QdrantDocumentStore.DeleteByRelPathAsync()` behaviour.

### Deterministic Point IDs

Same formula as .NET `IngestJob._StableId()`:

```python
def _stable_chunk_id(rel_path: str, breadcrumb: str, start_line: int) -> int:
    raw = f"{rel_path}|{breadcrumb}|{start_line}".encode()
    h = hashlib.blake2b(raw, digest_size=8)
    return int.from_bytes(h.digest(), "big")
```

---

## HTTP API Contract

Identical to the .NET contract (see `tech-details-dotnet.md` § HTTP API Contract).
Implemented in `ingest_routes.py` using Starlette `Route` objects.

### `POST /ingest/{collection}`

Request body (JSON):
```json
{ "relPath": "docs/adr/...", "content": "# Markdown...", "docKind": "adr" }
```

| Status | Condition |
|---|---|
| `202 Accepted` | Job enqueued; body contains `{operation_id, status, location}` |
| `400 Bad Request` | `rel_path` or `content` missing |
| `503 Service Unavailable` | Queue full (`asyncio.Queue.full()` is `True`) |

Response `202`:
```json
{
  "operation_id": "3fa85f64-...",
  "status": "Queued",
  "location": "/ingest/{collection}/operations/{operation_id}"
}
```
`Location` HTTP header also set (mirrors .NET `CreatedAtAction`).

### `GET /ingest/{collection}/operations/{operationId}`

Returns `IngestOperation` JSON (see Data Model above).  
`404` when `operationId` not found **or** `collection` does not match.

### `GET /ingest/{collection}/operations`

```json
{ "operations": [...], "count": 3 }
```

### `GET /admin/stats`

```json
{
  "queue_depth": 2,
  "retention_hours": 1,
  "total_operations": 17
}
```

---

## API Key Authentication (`api_key_middleware.py`)

`ApiKeyMiddleware` extends Starlette's `BaseHTTPMiddleware`:

- Checks `X-Api-Key` header on all paths starting with `/ingest/` or `/admin/`.
- Returns `401` if key missing or wrong.
- No-op when `RAG_API_KEY` env var is empty (local dev without auth).
- MCP transport paths (`/`, `/mcp`, `/sse`, `/messages/`) are **not** protected.

```bash
# Example: set auth key
export RAG_API_KEY="my-secret-key"

# Example: push with key
python tools/rag/ingest.py --remote http://localhost:3002 --api-key my-secret-key
```

---

## Multi-Tenant Session Binding (`?project=` → `ContextVar`)

When the MCP connection URL contains `?project=<name>` (HTTP Streamable or legacy SSE), the server binds
a per-session collection override using a Python `contextvars.ContextVar`:

```python
# state.py — shared deferred globals
_session_collection: ContextVar[str | None] = ContextVar("_session_collection", default=None)

# mcp_server.py — context manager wraps every HTTP Streamable / legacy SSE handler
@contextlib.contextmanager
def _bind_session_project(project: str | None):
    token = state._session_collection.set(project)
    try:
        yield
    finally:
        state._session_collection.reset(token)

async def handle_sse(request):
    project = request.query_params.get("project")
    with _bind_session_project(project):
        async with sse.connect_sse(...) as streams:
            await SERVER.run(...)
```

Tool handlers (`_tool_query_docs`, `_tool_read_docs`) in `rag_tools.py` pass
`collection=state._session_collection.get(None)` to `QueryEngine.search()`. When `None`,
`search()` falls back to `cfg.collection` (the default configured collection).

This mirrors the .NET scoped `RagSession` approach — each MCP connection gets its
own asyncio task and thus its own `ContextVar` token.

---

## DI / Wiring (HTTP / SSE branch of `mcp_server.py`)

```python
# mcp_server.py — lean entry point (~250 lines)
# Shared globals live in state.py; tool handlers live in rag_tools.py.
_store = OperationStore()
_queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)
_worker = IngestWorker(_queue, _store, state.ENGINE, state.CFG)

@contextlib.asynccontextmanager
async def lifespan(_app):
    _worker.start()   # creates asyncio.Task
    yield
    await _worker.stop()  # cancels task, awaits cleanup

app = Starlette(lifespan=lifespan, routes=[
    Route("/sse", endpoint=handle_sse),
    Mount("/messages/", app=sse.handle_post_message),
    *build_ingest_routes(_store, _queue, DEFAULT_CAPACITY),
])
app.add_middleware(ApiKeyMiddleware)
```

> **Module split (May 2026):** `mcp_server.py` was refactored — deferred globals moved to
> `state.py`; the 4 MCP tool coroutines moved to `rag_tools.py`. The lean `mcp_server.py`
> contains only the MCP `Server` object, the dispatch dict, transport wiring, and `__main__` startup.

---

## `ingest.py --remote` Flag

```bash
# Push all changed docs to a running Python MCP server
python tools/rag/ingest.py --remote http://localhost:3002 [--api-key KEY]

# Incremental: only changed/new files are pushed
python tools/rag/ingest.py --remote http://localhost:3002 --force-full
```

Behaviour:
- Uses stdlib `urllib.request` (no extra dependency).
- Retries on `503` up to 3 times with exponential back-off (1s, 2s).
- Polls `GET /ingest/{collection}/operations/{id}` every 2 seconds until
  `Completed` or `Failed`, or 120-second timeout.
- Exits with code `0` if all files succeeded, `1` if any failed.

---

## Implementation Status

| Step | Status |
|---|---|
| `operation_store.py` — in-memory OperationStore, 1h TTL | ✅ Implemented |
| `ingest_worker.py` — asyncio.Queue + Task consumer | ✅ Implemented |
| `ingest_routes.py` — Starlette POST/GET/admin routes | ✅ Implemented |
| `api_key_middleware.py` — X-Api-Key middleware | ✅ Implemented |
| `state.py` — shared deferred globals (ENGINE, CFG, _session_collection, TOOL_TIMEOUT) | ✅ Implemented |
| `rag_tools.py` — 4 MCP tool coroutines (query_docs, read_docs, get_history, list_adrs) | ✅ Implemented |
| `mcp_server.py` — lean HTTP / SSE wiring + dispatch dict + `_bind_session_project` CM | ✅ Implemented |
| `query.py` — `collection=` param on `search()` + `get_collection_config()` method | ✅ Implemented |
| `ingest.py` — `--remote` + `--api-key` flags | ✅ Implemented |
| Python unit tests | ✅ Implemented |
| Python E2E integration tests | ✅ Implemented |
| Qdrant-backed OperationStore (Phase 2 future) | ⬜ Deferred |

---

## Conformance Checklist

- [ ] `POST /ingest/{collection}` returns `202` with `operationId` and `Location` header
- [ ] `GET /ingest/{collection}/operations/{id}` returns `404` when `operationId` from
      a different collection is used
- [ ] `POST /ingest/{collection}` returns `503` when `_queue.full()` is `True`
- [ ] Re-ingesting the same `relPath` removes old Qdrant points before upserting
- [ ] `admin/stats.retention_hours` matches `RETENTION_HOURS` constant
- [ ] `RAG_API_KEY` unset → no auth on `/ingest/*`
- [ ] `RAG_API_KEY` set → `401` for wrong/missing key
- [ ] `?project=<name>` routes tool search to correct Qdrant collection
