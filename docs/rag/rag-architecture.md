# RAG Architecture — Current Dual-Stack Implementation

> **Status:** Reflects the system that actually ships as of May 2026. Two parallel
> implementations (Python in [`tools/rag/`](../../tools/rag/), .NET in
> [`tools/rag-dotnet/`](../../tools/rag-dotnet/)) expose the same four MCP tools
> against a shared [Qdrant](https://qdrant.tech/) vector database, with optional
> remote ingest over HTTP.
>
> For the **how-to** (install, run, query), see [`SETUP-GUIDE.md`](SETUP-GUIDE.md).
> For the **remote-ingest decision record** and per-stack tech details, see
> [ADR-0028](../adr/0028/0028-remote-multitenant-rag-ingest.md) +
> [`tech-details-dotnet.md`](../adr/0028/tech-details-dotnet.md) +
> [`tech-details-python.md`](../adr/0028/tech-details-python.md).
> A superseded v2 design proposal is preserved at
> [`rag-architecture-v2-design.md`](rag-architecture-v2-design.md) for historical reference only.

---

## 1. Scope

This document covers what is implemented and operational:

- Two MCP server stacks (Python, .NET) — identical tool surface, different runtimes.
- Shared Qdrant container; **one collection per stack** (`ecommerceapp_docs`, `ecommerceapp_docs_dotnet`).
- Three transports per stack: STDIO (default), HTTP Streamable (persistent server), legacy SSE (alias).
- Local ingest CLIs and remote ingest over `POST /ingest/{collection}` (single + ZIP batch).
- Session-bound project selection via `?project=<collection>` on HTTP URLs.
- ApiKey middleware + sanitised JSON error envelope shared between stacks.

Out of scope (not implemented): the v2 single-shared-server / `.copilot-rag.yaml`
manifest design, the `/rag-sync` agent, the eval pipeline, multi-repo `tune_weights.py`.
See [`rag-architecture-v2-design.md`](rag-architecture-v2-design.md) and §15.

---

## 2. System overview

```text
        ┌─────────────────────────────┐         ┌──────────────────────────────┐
        │  Docs source (this repo)    │         │  VS Code + Copilot Chat      │
        │  docs/**, .github/context/  │         │  .vscode/mcp.json picks ONE  │
        └──────────────┬──────────────┘         │  server variant per session  │
                       │                        └──────────────┬───────────────┘
                       │  ingest CLI                           │  MCP (stdio | HTTP)
                       │  (local or --remote)                  │
                       ▼                                       ▼
        ┌──────────────────────────────┐        ┌──────────────────────────────┐
        │  Python stack  (tools/rag/)  │        │  .NET stack (tools/rag-dotnet)│
        │  - ingest.py                 │        │  - RagTools.Ingest           │
        │  - mcp_server.py             │        │  - RagTools.Mcp              │
        │  - ONNX embedder (sentence-  │        │  - ONNX embedder (same model,│
        │    transformers tokenizer)   │        │    BERT WordPiece workaround)│
        │  - tiktoken cl100k_base      │        │  - .NET tokenizer            │
        └──────────────┬───────────────┘        └───────────────┬──────────────┘
                       │                                        │
                       │   gRPC :6334 + HTTP :6333              │
                       └────────────────┬───────────────────────┘
                                        ▼
                            ┌───────────────────────┐
                            │ Qdrant v1.13.6        │
                            │ (single container)    │
                            │                       │
                            │ ecommerceapp_docs     │  ← Python writes
                            │ ecommerceapp_docs_dotnet │← .NET writes
                            └───────────────────────┘
```

Both stacks share the same model family
(`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`, 384-dim) and the same
chunking strategy, so the two collections answer the same questions but are not
interchangeable — embeddings are tokenizer-dependent.

---

## 3. Components per stack

### Python stack — [`tools/rag/`](../../tools/rag/)

| File | Role |
|---|---|
| [`mcp_server.py`](../../tools/rag/mcp_server.py) | MCP entry: STDIO (default), HTTP Streamable (`_run_http`), legacy SSE (`_run_sse`, alias) |
| [`ingest.py`](../../tools/rag/ingest.py) | Local ingest CLI; supports `--remote http://...` to push docs to a running server |
| [`ingest_routes.py`](../../tools/rag/ingest_routes.py) | Starlette routes: `POST /ingest/{collection}`, `POST /ingest/{collection}/batch`, `GET /ingest/{collection}/operations[/{opId}]`, `GET /admin/stats` |
| [`ingest_worker.py`](../../tools/rag/ingest_worker.py) | In-process worker: pulls from `asyncio.Queue`, chunks + embeds + upserts. Runs `tiktoken` (`cl100k_base`) at import time |
| [`operation_store.py`](../../tools/rag/operation_store.py) | In-memory job status tracking; 1-hour retention |
| [`chunker.py`](../../tools/rag/chunker.py) | Markdown heading-aware chunker (uses `tiktoken`) |
| [`api_key_middleware.py`](../../tools/rag/api_key_middleware.py) | Starlette middleware enforcing `X-Api-Key` on `/ingest/*` and `/admin/*` |
| [`query.py`](../../tools/rag/query.py) | Query engine — semantic search, weight application, breadcrumb prefixing |
| [`rag_tools.py`](../../tools/rag/rag_tools.py) | Tool implementations dispatched from `mcp_server.py` |
| [`state.py`](../../tools/rag/state.py) | `_session_collection: ContextVar[str | None]` — per-session collection override from `?project=` |
| [`remote_ingest_client.py`](../../tools/rag/remote_ingest_client.py) | HTTP client used by `ingest.py --remote` |
| [`multilingual-glossary.yaml`](../../tools/rag/multilingual-glossary.yaml) | Query-time PL/DE → EN expansion table |
| [`rag-config.yaml`](../../tools/rag/rag-config.yaml) | Source roots, embedder, chunker, vector store, weights |
| [`metadata-rules.yaml`](../../tools/rag/metadata-rules.yaml), [`queries.yaml`](../../tools/rag/queries.yaml) | Companion config files (classification, named queries) |

Runtime: Python 3.13, `mcp >= 1.8.0`, Starlette + uvicorn, ONNX runtime,
`sentence-transformers` tokenizer, `tiktoken`.

### .NET stack — [`tools/rag-dotnet/`](../../tools/rag-dotnet/)

Three assemblies under `src/`:

| Assembly | Role |
|---|---|
| `RagTools.Core` | Embedder, tokenizer, chunker, query engine, ingest pipeline, batch validator, glossary expansion |
| `RagTools.Mcp` | MCP server entry ([`Program.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Program.cs)); STDIO via `Host.CreateApplicationBuilder`, HTTP via `WebApplication.CreateBuilder`. Controllers + middleware below. |
| `RagTools.Ingest` | CLI ingest entry; supports `--remote http://...:3001` |

Key types inside `RagTools.Mcp`:

| Type | Role |
|---|---|
| [`Controllers/IngestController.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Controllers/IngestController.cs) | ASP.NET Core controller for `/ingest/{collection}` + batch + operations |
| [`Middleware/ApiExceptionHandler.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiExceptionHandler.cs) | `IExceptionHandler` — converts unhandled exceptions to the sanitised JSON envelope (§14) |
| [`Middleware/BadRequestEnvelopeMiddleware.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs) | Rewrites `ProblemDetails` 400 responses into the same `{error, code}` envelope |
| [`Middleware/ApiKeyMiddleware.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiKeyMiddleware.cs) | Enforces `X-Api-Key` on `/ingest/*` and `/admin/*` |
| [`Tools/RagTools.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs) | `[McpServerTool]` methods — `list_adrs`, `query_docs`, `read_docs`, `get_history` |
| [`Tools/McpToolGuard.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs) | `RunAsync(...)` wrapper around every tool — sanitises errors using `ToolErrorSanitizer` |
| [`Tools/RagSession.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Tools/RagSession.cs) | DI-scoped collection name (set per HTTP request from `?project=`) |
| `IngestWorker` (`BackgroundService`) | Same role as Python's `ingest_worker.py` — `Channel<IngestJob>` consumer |
| `OperationStore` (`ConcurrentDictionary`) | In-memory job tracking; same 1-hour retention |

Runtime: .NET 10, `ModelContextProtocol 1.3.0` SDK, ASP.NET Core + Kestrel,
ONNX runtime, custom BERT WordPiece adapter for the multilingual model.

---

## 4. Transports

Both stacks support three transports selected at startup:

| Transport | Selected by | URL / pipe | Used by |
|---|---|---|---|
| **STDIO** | default | stdin/stdout pipe | VS Code launches the process per chat session (`type":"stdio"` in `mcp.json`) |
| **HTTP Streamable** | `MCP_TRANSPORT=http` | `http://localhost:3001/` (.NET), `http://localhost:3002/` (Python) | Persistent server; VS Code reconnects (`"type":"http"`) |
| **Legacy SSE** | `MCP_TRANSPORT=sse` (alias) | `GET /sse` + `POST /messages/` | Backward compatibility only — prefer HTTP Streamable for all new setups |

HTTP Streamable is **POST-based** (MCP spec 1.x). The Python entry is
`_run_http()` in [`mcp_server.py`](../../tools/rag/mcp_server.py)
(`StreamableHTTPSessionManager`). The .NET entry calls
`builder.Services.AddMcpServer().WithHttpTransport()` and `app.MapMcp("/")`.

Container ports are pinned in [`docker-compose.yaml`](../../docker-compose.yaml):

```yaml
rag-python-http:  ports: ["3002:3002"]   profiles: [rag-python-http]
rag-dotnet-http:  ports: ["3001:3001"]   profiles: [rag-dotnet-http]
qdrant:           ports: ["6333:6333", "6334:6334"]
```

---

## 5. Vector store

A single Qdrant container (image `qdrant/qdrant:v1.13.6`) is shared by both stacks.
HTTP API on `:6333`, gRPC on `:6334` (used by the .NET `Qdrant.Client` package).

Each stack writes to **its own collection** because embeddings are tokenizer-dependent
and the two stacks use different tokenizers for the same model:

| Stack | Collection | Tokenizer |
|---|---|---|
| Python | `ecommerceapp_docs` | `sentence-transformers` (matches model) |
| .NET | `ecommerceapp_docs_dotnet` | Custom BERT WordPiece adapter |

Switching the model requires `--force-full` re-ingest on both stacks; the
`rag-config.yaml › version` field auto-triggers a full rebuild when bumped.

---

## 6. Configuration files

Each stack has its **own** `rag-config.yaml`. They share the same schema but
point at different collections by default:

| File | Used by |
|---|---|
| [`tools/rag/rag-config.yaml`](../../tools/rag/rag-config.yaml) | Python ingest + MCP server |
| [`tools/rag-dotnet/rag-config.yaml`](../../tools/rag-dotnet/rag-config.yaml) | .NET ingest + MCP server |

Companion files in `tools/rag/` (`metadata-rules.yaml`, `queries.yaml`,
`multilingual-glossary.yaml`) are mounted into both Docker images.

Resolution priority: `--config <path>` CLI flag → `RAG_CONFIG` env var → default
path next to the entry executable. The .NET server uses
`RagConfig.ResolveConfigPath(null)` and exits with a clear error if no config can
be located.

---

## 7. Ingest pipelines

### 7a. Local (default)

```text
ingest CLI ──► chunker ──► embedder ──► Qdrant.Client ──► Qdrant
   │                                              ▲
   └──► .rag/manifest.json (sha256 per file) ─────┘  (incremental ingest)
```

- Hash-based incremental ingest: only files whose `sha256` changed are re-embedded.
- `--force-full` rebuilds the whole collection.
- Manifest is gitignored; lives at `.rag/manifest.json`.

### 7b. Remote (`--remote http://host:port`)

The same CLI streams documents to a running HTTP server instead of touching
Qdrant directly. The server enqueues each document into its in-process worker
and returns immediately with an `operationId`.

```text
ingest --remote ──► POST /ingest/{collection}    (one doc per request)
                or POST /ingest/{collection}/batch  (ZIP archive, many docs)
                                │
                                ▼
                       OperationStore.enqueue()
                                │   status: Queued
                                ▼
                      IngestWorker (background)
                                │   status: Processing
                                ▼
                       chunk + embed + upsert  ────► Qdrant
                                │
                                ▼
                       mark_completed | mark_failed
```

Status polling:

| Endpoint | Returns |
|---|---|
| `GET /ingest/{collection}/operations/{opId}` | One operation's current state |
| `GET /ingest/{collection}/operations` | All recent operations for a collection |

Status retention: 1 hour after enqueue (`RETENTION_HOURS = 1` in Python,
`TimeSpan.FromHours(1)` in .NET).

Queue capacity: 100 (Python) / 1000 (.NET). Server returns `503 Service
Unavailable` when full.

The full HTTP API reference (request/response schemas, error envelope) lives in
[`SETUP-GUIDE.md` § Remote ingest mode](SETUP-GUIDE.md#remote-ingest-mode---remote).

---

## 8. MCP tools

Both stacks expose the same four tools. Names are identical; argument shapes
match.

| Tool | Input | Returns |
|---|---|---|
| `list_adrs` | none | All ADRs in the collection (id, title, amendment/example counts) |
| `query_docs(question, bc?, top_k=5)` | semantic query, optional bounded-context substring filter | Ranked chunks with `rel_path`, `breadcrumb`, `lines`, `score`, `weight`, `text` |
| `read_docs(question, bc?, top_files=3)` | semantic query | Best chunks grouped by file; switches to full-file content when the query contains explicit "full content" intent phrases |
| `get_history(id)` | history group identifier (e.g. ADR number) | All chunks for that group, sorted by `start_line` |

`get_history` replaced the older `get_adr_history` (parameter `adr_id` →
`id`); the grouping field is collection-configurable (defaults to `adr_id`).

Tool dispatch is wrapped in error-sanitising guards on both stacks — see §14.

---

## 9. Multilingual query expansion

The active model is multilingual but mean-pooling biases retrieval toward English
when a non-English query has a stronger English neighbour cluster. Before
embedding, the query is matched against `multilingual-glossary.yaml` and matching
English terms are appended **3×** so they dominate pooling.

This runs **at query time only** — no re-indexing required. Both stacks share
the same glossary file and produce equivalent recall on the benchmark suite
(2026-05-19: EN 5/5, PL 5/5, DE 3-4/5 top-1).

---

## 10. Weight system

After Qdrant similarity search, each hit's score is multiplied by a path-based
weight. First-matching glob wins. Weights live in each stack's
`rag-config.yaml › ranking.weights`. Higher weights win ties.

Current defaults (both stacks):

| Weight | Pattern | Why |
|---|---|---|
| 1.25 | `.github/context/known-issues.md` | Bug-fix gate |
| 1.20 | `.github/context/agent-decisions.md`, `docs/adr/*/amendments/**` | Corrections + amendments override originals |
| 1.15 | `.github/context/project-state.md` | BC block status |
| 1.10 | `docs/adr/*/example-implementation/**` | Concrete examples |
| 1.00 | `docs/adr/*/[0-9]*-*.md` | Main ADR |
| 0.95 | `docs/adr/*/README.md` | ADR router pages |
| 0.70 | `docs/roadmap/**` | Forward-looking, lower priority for "how" questions |
| 0.40 | `docs/adr/*/checklist.md` | Indexed but rarely the primary answer |
| 0.30 | `docs/adr/*/migration-plan.md` | Same |
| 0.20 | `docs/rag/**` | RAG meta-docs |

There is **no automated tune step** today — weights are edited by hand. The
v2 design's `tune_weights.py` was not built.

---

## 11. Session-bound project selection (`?project=`)

HTTP MCP URLs accept a `?project=<collection>` query parameter. When VS Code
opens a session against
`http://localhost:3001/?project=ecommerceapp_docs_dotnet`, every tool call in
that session targets that collection — no per-call argument needed.

| Stack | Mechanism |
|---|---|
| Python | `ContextVar[str | None]` in [`state.py`](../../tools/rag/state.py); per-request token lifecycle around the MCP session |
| .NET | DI-scoped `RagSession.Collection` populated from the query string |

Default (no `?project=`) falls back to the collection in `rag-config.yaml`.

> **Known asymmetry:** Python `list_adrs` currently ignores the `?project=`
> override and uses the collection from config; `query_docs` / `read_docs` /
> `get_history` honour it. The .NET stack honours it everywhere. Not blocking
> for single-project use; tracked for future cleanup.

---

## 12. Testing harness

| Stack | Suite | Count | Command |
|---|---|---|---|
| .NET | All (unit + integration + E2E) | 478 | `dotnet test tools/rag-dotnet/RagTools.sln -c Release` |
| Python | Unit | 204 | `tools/rag/.venv/Scripts/python.exe -m pytest tools/rag/tests/ -q --ignore=tools/rag/tests/test_e2e.py` |
| Python | E2E | 89 | `pytest tools/rag/tests/test_e2e.py -v` |
| Cross-stack pipeline | manual harness | — | `python tools/rag/real_mcp_check.py` (Phase A STDIO, Phase B HTTP CLI, Phase C HTTP raw) |

Convenience scripts: [`tools/rag/run-tests.ps1`](../../tools/rag/run-tests.ps1),
[`tools/rag-dotnet/run-tests.ps1`](../../tools/rag-dotnet/run-tests.ps1),
[`tools/rag/run-all-tests.ps1`](../../tools/rag/run-all-tests.ps1).

---

## 13. Operational notes

- **Always start Qdrant first.** Both HTTP servers `depends_on: qdrant`, but
  switching docker-compose profiles can leave orphan containers holding
  `:3001` / `:3002`. Run `docker compose --remove-orphans down` before switching,
  or use `--force-recreate` on `up`.
- **First Python container start** downloads the `cl100k_base` BPE for
  `tiktoken` at module-import time inside `ingest_worker.py`. If the container
  network is half-wired (orphan port-binding race), this fails with
  `Temporary failure in name resolution`. The model can be pre-baked into the
  image: `RUN python -c "import tiktoken; tiktoken.get_encoding('cl100k_base')"`.
- **The two stacks are independent:** running ingest on one does not update the
  other. Re-index both after large doc rewrites if both are in use.
- **Stdout discipline:** both STDIO entries write logs to **stderr only** to
  avoid corrupting MCP framing on stdout.

---

## 14. Error handling, sanitisation, and middleware

All HTTP responses from both stacks (auth failures, malformed bodies, unhandled
exceptions, MCP tool failures) follow the same envelope:

```json
{ "error": "<sanitised message>", "code": "<bucket>" }
```

Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`,
`InternalServerError`. Messages are capped at 500 characters and absolute
filesystem paths are stripped (`C:\...\file.cs` → `<path>/file.cs`). Stack
traces stay in server logs and are **never** returned to clients.

### .NET layering ([`tools/rag-dotnet/src/RagTools.Mcp/Program.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Program.cs))

```csharp
app.UseExceptionHandler();                          // IExceptionHandler → ApiExceptionHandler
app.UseMiddleware<BadRequestEnvelopeMiddleware>();  // rewrites ProblemDetails 400 → envelope
app.UseMiddleware<ApiKeyMiddleware>();              // enforces X-Api-Key on /ingest/*, /admin/*
app.MapControllers();                               // IngestController
app.MapMcp("/");                                    // MCP HTTP Streamable surface
```

Tool methods on `[McpServerTool]` classes wrap their body in
`McpToolGuard.RunAsync(...)`, which catches all exceptions, runs them through
`ToolErrorSanitizer`, and returns the same envelope payload.

### Python layering ([`tools/rag/mcp_server.py`](../../tools/rag/mcp_server.py))

- `_install_exception_handlers(app)` registers Starlette global handlers that
  mirror `ApiExceptionHandler` (passes `HTTPException` through so explicit 4xx
  responses keep their status codes).
- `ApiKeyMiddleware` from [`api_key_middleware.py`](../../tools/rag/api_key_middleware.py)
  enforces `X-Api-Key` on `/ingest/*` and `/admin/*`.
- `@server.call_tool()` handlers run inside a try/except that sanitises via
  `_sanitize_error_message(exc)`. The `_PATH_RE` regex strips drive letters and
  POSIX paths the same way `ToolErrorSanitizer` does.

### Adding new endpoints / tools

- **.NET:** put new HTTP endpoints in a controller; do not bypass
  `ApiExceptionHandler` / `BadRequestEnvelopeMiddleware`. New tool methods on
  `[McpServerTool]` types **must** call `McpToolGuard.RunAsync(...)`.
- **Python:** new Starlette routes are covered automatically by the global
  handlers in `_install_exception_handlers`. New `@server.call_tool()` handlers
  must stay inside the existing try/except guard (do not raise out of the
  handler — wrap into a sanitised return).

`RAG_API_KEY` env var on the server enables auth. When unset, the middleware
runs in dev mode and accepts all `/ingest/*` and `/admin/*` calls. Querying via
MCP is never gated by `RAG_API_KEY` — it is assumed to happen inside a trusted
network (see ADR-0028).

---

## 15. Future directions

Items previously sketched in [`rag-architecture-v2-design.md`](rag-architecture-v2-design.md)
that may inform later work:

- **Shared `rag-tools` repository** — extract `tools/rag/` and `tools/rag-dotnet/`
  to standalone repos installable per project.
- **Hash-driven incremental ingest already exists for local mode**;
  extending it to incremental remote ingest is a candidate optimisation.
- **`/rag-sync` agent + eval pipeline** — not built. The Q&A regression set
  currently lives only in the manual harness (`real_mcp_check.py`).
- **MSSQL 2025 vector store** — listed as an evaluation target; would replace
  Qdrant if MSSQL 2025's vector type meets recall/latency requirements.
- **`?project=` symmetry fix for Python `list_adrs`** — small follow-up.
- **Pre-bake `tiktoken` cache in the Python Dockerfile** — eliminates the
  network-failure-on-first-run class of issue noted in §13.
