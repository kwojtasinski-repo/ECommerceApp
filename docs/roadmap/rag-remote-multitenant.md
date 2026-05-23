# Roadmap: Remote Multi-Tenant RAG Server (ADR-0028)

> Status: ✅ Phase 1 Complete — Phase 2 in progress (P2-1 ✅, P2-2 ✅)  
> Scope: `tools/rag-dotnet/` (.NET server), `tools/rag/` (Python server)  
> ADR: [ADR-0028](../adr/0028/0028-remote-multitenant-rag-ingest.md)

---

## Implementation Status (Phase 1: .NET)

| Step | Description | Status | Notes |
|---|---|---|---|
| 0 | `IDocumentStore`, `QdrantDocumentStore`, `ContentDocument`, `DocKind` | ✅ Done | Committed `3a71f970` |
| 1 | `DeterministicId`, `RagPayload` + `ChunkIndex`/`ContentId` | ✅ Done | Committed `3a71f970` |
| 2 | `StoreDocumentAsync` / `FetchContentAsync` (zero-vector content point) | ✅ Done | Committed `3a71f970` |
| 3 | `RagConfigPayload`, `StoreConfigAsync`, `FetchConfigAsync` | ✅ Done | Committed `3a71f970` |
| 4 | `QueryCache` + `CachedDocumentStore` decorator | ✅ Done | In-memory, bounded 512 entries |
| 5 | `IngestJob`, `IngestChannel` (bounded `Channel<T>`) | ✅ Done | Capacity 1000, FIFO |
| 6 | `IngestWorker` (BackgroundService) + `OperationStore` | ✅ Done | **In-memory** (not Qdrant-backed — see deviation note) |
| 7 | `IngestController` (POST/GET /ingest/{collection}) | ✅ Done | 202 Accepted + Location header |
| 8 | `ApiKeyMiddleware` (X-Api-Key, RAG_API_KEY) | ✅ Done | Guards /ingest/* and /admin/* |
| 9 | `RagSession` + `RagSessionMiddleware` (?project= collection) | ✅ Done | Scoped DI in SSE mode |
| 10 | `read_docs` uses `IDocumentStore.FetchContentAsync` → disk fallback | ✅ Done | Tools use `IDocumentStore` + `RagSession` |
| 11 | CLI remote mode (`--remote <url>`) | ✅ Done | HTTP upload, manifest tracking |

### Deviations from original design

**Step 6 — OperationStore**: The roadmap specified Qdrant-backed `__ops__` collection.
Implemented as in-memory `ConcurrentDictionary<string, IngestOperationResult>` with 1-hour TTL
retention and access-time eviction. Rationale: simpler, no extra Qdrant overhead, sufficient
for the polling use case. Operations do not survive server restart (acceptable — clients re-ingest
on restart anyway). Documented in ADR-0028 amendment.

**Step 5 — IngestJob**: Roadmap modeled `IngestJob` with `IReadOnlyList<(string RelPath, string Text)> Files`
(multiple files per job). Implemented as single-file-per-job for simpler channel semantics and
better per-file operation tracking. The HTTP API accepts one document per POST request.

---

## Problem

The current RAG server (ADR-0027) requires the workspace to be mounted as a filesystem
volume and uses a single fixed collection per server instance. This prevents shared-team
deployment where docs live on developer machines and the server runs remotely.

**Blockers without this work:**
- Cannot deploy a shared RAG server (no volume mount on remote host)
- Serving 2 projects requires 2 containers × 470 MB ONNX model each
- `read_docs` reads files from disk — fails completely if filesystem is unavailable

---

## Target state (post ADR-0028)

```
Developer machine              Remote server (one instance, one ONNX model)
─────────────────              ──────────────────────────────────────────────
docs/                          POST /ingest/ecommerceapp  (async, returns opId)
config.yaml    ─[ingest CLI]─► background worker: chunk + embed + upsert to Qdrant
                               GET /ingest/ecommerceapp/operations/{opId}  (poll)

VS Code (.vscode/mcp.json)     MCP SSE endpoint
  url: .../?project=ecommer ─► session bound to "ecommerceapp" collection
  app  ──────────────────────► query_docs / read_docs / list_adrs / get_adr_history
                                 all data from Qdrant — zero filesystem access
```

---

## Implementation Plan — Phase 1: .NET Server

### Step 0 — `IDocumentStore` interface and `QdrantDocumentStore` (foundational, do first)

**New file**: `tools/rag-dotnet/src/RagTools.Core/IDocumentStore.cs`

Define the storage abstraction that all tools and the ingest worker depend on:

```csharp
public interface IDocumentStore
{
    // Ingest path
    Task UpsertChunksAsync(string collection, IEnumerable<ChunkPoint> chunks, CancellationToken ct);
    Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct);
    Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct);
    Task StoreGlossaryAsync(string collection, string[] terms, CancellationToken ct);

    // Query path
    Task<IReadOnlyList<SearchResult>> SearchAsync(string collection, float[] vector, SearchOptions opts, CancellationToken ct);
    Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct);
    Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct);
    Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct);
}
```

**New file**: `tools/rag-dotnet/src/RagTools.Core/ContentDocument.cs`

```csharp
public sealed record ContentDocument(
    string RelPath,
    string DocKind,
    string? Bc,
    string? Title,
    string Content,
    DateTimeOffset IngestedAt,
    IDictionary<string, string>? Metadata = null);
```

**New file**: `tools/rag-dotnet/src/RagTools.Core/QdrantDocumentStore.cs`

The first implementation of `IDocumentStore` backed by Qdrant. Registered in DI as:
```csharp
services.AddSingleton<IDocumentStore, QdrantDocumentStore>();
```

All existing direct Qdrant calls in `QdrantStore.cs` are migrated here or wrapped.
Tools and ingest worker must NEVER call `QdrantClient` directly — always via `IDocumentStore`.

---

### Step 1 — Qdrant payload schema (breaking change, do first)

**File**: `tools/rag-dotnet/src/RagTools.Core/QdrantStore.cs`

Add a constant or enum for reserved `doc_kind` values:

```csharp
public static class DocKind
{
    public const string Chunk       = "chunk";       // existing (may need rename from current value)
    public const string FullContent = "full_content";
    public const string Config      = "__config__";
    public const string Glossary    = "__glossary__";
    public const string Rules       = "__rules__";
    public const string Queries     = "__queries__";
    public const string Operation   = "__op__";
}
```

Add a search filter helper that excludes all reserved `doc_kind` values from results.  
Existing `SearchAsync` must be updated to apply this filter by default.

Chunk point IDs are deterministic: `UUID(MD5("{collection}:{relPath}:{chunk_index}"))`
Content point IDs are deterministic: `UUID(MD5("{collection}:{relPath}"))`
Both computed in a shared `DeterministicId` helper class.

**Risk**: If existing collection was ingested without explicit `doc_kind: "chunk"` on chunk
points, the filter will hide them. Must verify what the current ingest writes.  
**Mitigation**: Check `RagTools.Ingest/Program.cs` payload construction before adding filter.

---

### Step 2 — Full content storage via `IDocumentStore.StoreDocumentAsync`

**File**: `tools/rag-dotnet/src/RagTools.Core/QdrantDocumentStore.cs`

Implement `StoreDocumentAsync` and `FetchContentAsync` in `QdrantDocumentStore`:

```csharp
// Upsert one full-content point per file (zero vector, doc_kind = "full_content")
// Point ID = DeterministicId.ForContent(collection, relPath)
// Payload is structured JSON (ContentDocument), not raw YAML text
public Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct);

// Fetch full ContentDocument for a given relPath
// After vector search: collect content_ids from hit chunk payloads
// → one batch GetPointsAsync([id1, id2, ...]) — O(1), no second search
public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct);
```

Every chunk point payload must include:
```json
{
  "doc_kind": "chunk",
  "relPath": "docs/adr/0016/...",
  "chunk_index": 4,        ← stored (required for neighbor computation)
  "text": "...",
  "content_id": "a3f9c1b2-..."  ← UUID(MD5("{collection}:{relPath}"))
}
```

Context expansion (±N neighbors) uses computed IDs — no stored prev/next pointers:
```csharp
var neighborIds = Enumerable.Range(chunkIndex - 2, 5)
    .Select(i => DeterministicId.ForChunk(collection, relPath, i))
    .ToList();
```

---

### Step 3 — Config/glossary/queries round-trip in Qdrant (structured JSON, not raw YAML)

**File**: `tools/rag-dotnet/src/RagTools.Core/QdrantDocumentStore.cs`

Implement `StoreConfigAsync` and `FetchConfigAsync` in `QdrantDocumentStore`:

```csharp
public Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct);
public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct);
// Similarly for glossary, metadata-rules, queries.yaml
```

**Critical**: YAML is parsed **once at upload time** and stored as structured JSON:
```json
{
  "doc_kind": "__config__",
  "schema_version": 1,
  "chunker": { "max_tokens": 512, "overlap": 64 },
  "weights": { "adr": 1.2, "context": 1.15 },
  "score_threshold": 0.35,
  "fetch_k": 20
}
```

Not raw YAML text. YAML format errors during upload → `400 Bad Request` immediately.  
Server reads back structured JSON at startup — no YAML parser needed at runtime.

Include `queries.yaml` in the set of uploaded config files. Stored as `doc_kind: "__queries__"`
with the parsed named eval query list as structured JSON.

---

### Step 4 — Caching layer: `QueryCache` + `CachedDocumentStore`

**New file**: `tools/rag-dotnet/src/RagTools.Core/QueryCache.cs`

Holds embedding cache and ADR list cache:
```csharp
public sealed class QueryCache(IMemoryCache inner, CacheOptions opts)
{
    public float[] GetOrEmbed(string expandedText, Func<float[]> embed);
    public Task<IReadOnlyList<AdrSummary>> GetOrListAdrsAsync(string collection,
        Func<Task<IReadOnlyList<AdrSummary>>> fetch);
    public void InvalidateCollection(string collection);  // called on ingest
}
```

Cache TTLs from env vars (read at startup via `CacheOptions` bound from environment):
- `RAG_EMBED_CACHE_TTL_SECONDS` = 600 (10 min)
- `RAG_ADR_LIST_CACHE_TTL_SECONDS` = 3600 (1 hour)

**New file**: `tools/rag-dotnet/src/RagTools.Core/CachedDocumentStore.cs`

Decorator over `IDocumentStore` that caches content and chunk context:
```csharp
public sealed class CachedDocumentStore(IDocumentStore inner, IMemoryCache cache, CacheOptions opts)
    : IDocumentStore
{
    public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct);
    // ↑ cached with key "content:{collection}:{relPath}", TTL = RAG_CONTENT_CACHE_TTL_SECONDS (120 s)
    
    // All other IDocumentStore methods delegate to inner
    public void InvalidateCollection(string collection);  // remove all cached keys for this collection
}
```

Config and glossary cached with event-driven invalidation (no TTL expiry — cleared on ingest).
Safety-net TTL via `RAG_CONFIG_CACHE_TTL_SECONDS` = 3600 s.

Registered in DI:
```csharp
services.AddSingleton<QdrantDocumentStore>();
services.AddSingleton<IDocumentStore>(sp =>
    new CachedDocumentStore(sp.GetRequiredService<QdrantDocumentStore>(), ...));
```

Set any `RAG_*_CACHE_TTL_SECONDS` to `0` to disable that specific cache.

---

### Step 5 — Ingest background worker

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/Ingest/IngestWorker.cs`

```csharp
public sealed class IngestWorker(
    Channel<IngestJob> jobs,
    OnnxEmbedder embedder,
    IDocumentStore store,
    QueryCache queryCache,
    OperationStore ops) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in jobs.Reader.ReadAllAsync(ct))
        {
            await ops.SetStatusAsync(job.OperationId, "processing", ct);
            try
            {
                await ProcessJobAsync(job, ct);
                // Invalidate all caches for this collection after successful ingest
                queryCache.InvalidateCollection(job.Collection);
                ((CachedDocumentStore)store).InvalidateCollection(job.Collection);
                await ops.SetStatusAsync(job.OperationId, "completed", ct);
            }
            catch (Exception ex)
            {
                await ops.SetStatusAsync(job.OperationId, "failed", ex.Message, ct);
            }
        }
    }
    // ProcessJobAsync: iterate job.Files, chunk each, embed, upsert chunk + content points
    // Each chunk payload includes chunk_index and content_id
}
```

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/Ingest/IngestJob.cs`

```csharp
public sealed record IngestJob(
    string OperationId,
    string Collection,
    IReadOnlyList<(string RelPath, string Text)> Files,
    string? ConfigYaml,
    string? GlossaryYaml,
    string? MetadataRulesYaml,
    string? QueriesYaml);
```

---

### Step 6 — Operation status store

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/Ingest/OperationStore.cs`

Wraps a separate Qdrant collection (`__ops__`).  

```csharp
public sealed class OperationStore(QdrantClient client)
{
    public Task SetStatusAsync(string opId, string status, string? error = null, CancellationToken ct = default);
    public Task<OperationStatus?> GetStatusAsync(string opId, CancellationToken ct = default);
    public Task SweepExpiredAsync(CancellationToken ct = default);   // deletes expires_at < now
}
```

Scheduled sweep: register a `PeriodicTimer`-based `IHostedService` that calls `SweepExpiredAsync`
every 6 hours.

---

### Step 7 — Ingest API controller

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/Ingest/IngestController.cs`

SSE mode only (the server needs `MapControllers()` added to the pipeline).

```csharp
[ApiController]
[Route("ingest")]
public sealed class IngestController(
    Channel<IngestJob> jobs,
    OperationStore ops) : ControllerBase
{
    [HttpPost("{collection}")]
    public async Task<IActionResult> Upload(string collection, [FromForm] IngestUploadRequest request);
    // - validates X-Api-Key header
    // - reads IFormFileCollection + optional config files from request (incl. queries.yaml)
    // - reads all text content into memory
    // - enqueues IngestJob
    // - returns 202 with operationId

    [HttpGet("{collection}/operations/{operationId}")]
    public async Task<IActionResult> GetStatus(string collection, string operationId);
    // - validates X-Api-Key header
    // - calls ops.GetStatusAsync
    // - triggers background sweep
    // - returns 200 with OperationStatus DTO

    [HttpGet("{collection}/operations")]
    public async Task<IActionResult> ListOperations(string collection, [FromQuery] string? status = null);
    // - validates X-Api-Key header
    // - calls ops.ListAsync(collection, status)
    // - returns 200 with list of OperationStatus DTOs
}
```

**New minimal endpoint**: `GET /admin/stats`

```csharp
[ApiController]
[Route("admin")]
public sealed class AdminController(IDocumentStore store) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats();
    // Returns: collection names, chunk count, content MB, last_ingested_at, store impl name
    // No auth required (read-only health info)
}
```
```

---

### Step 8 — API key middleware

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiKeyMiddleware.cs`

Applied only to `/ingest/*` routes. Reads `RAG_API_KEY` from env. Rejects with 401 if
header is missing or doesn't match. Logs a startup warning if `RAG_API_KEY` is not set.

Not applied to MCP tool endpoints (SSE / stdio).

---

### Step 9 — Session-level project selection (SSE transport)

**File**: `tools/rag-dotnet/src/RagTools.Mcp/Program.cs`

In the SSE branch, extract `?project=` from the query string and bind it to the
`IHttpContextAccessor` / `RagSession` scoped service before the MCP handler runs.

**New file**: `tools/rag-dotnet/src/RagTools.Mcp/RagSession.cs`

```csharp
public sealed class RagSession
{
    public string Collection { get; set; } = string.Empty;
}
```

Registered as `Scoped`. MCP tool calls read the collection from `RagSession` instead of
`RagConfig.Collection` directly (when not empty; falls back to config default).

`QdrantStore` must become collection-aware per call (or `RagSession` is used to create
a per-session `QdrantStore` wrapper that specifies the collection).

---

### Step 10 — `read_docs` uses `IDocumentStore.FetchContentAsync`

**File**: `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs`

Modify `ReadDocs` to call `store.FetchContentAsync(collection, relPath, ct)` instead of
`File.ReadAllText(...)`. The `CachedDocumentStore` decorator provides the 2-min cache automatically.

Fetch is O(1) via `content_id` batch lookup (see §6 of ADR-0028).
Falls back to chunk stitching if no content point exists for a path
(handles the transitional period when existing ingested data has no content points yet).

---

### Step 11 — Ingest CLI remote mode + ops list

**File**: `tools/rag-dotnet/src/RagTools.Ingest/Program.cs`

Add `--remote <url>` flag:

```
dotnet run --project tools/rag-dotnet/src/RagTools.Ingest -- --remote http://rag.internal:3001 --workspace .
```

When `--remote` is provided:
1. Read collection name from `config.yaml` (already parsed)
2. Zip `docs/` + `.github/context/` + `config.yaml` + glossary + rules + `queries.yaml`
3. POST to `{remote}/ingest/{collection}` with `X-Api-Key` from `RAG_API_KEY` env
4. Poll `GET {remote}/ingest/{collection}/operations/{id}` every 3 s until `completed` or `failed`
5. Print progress

---

## Phase 2 — Design corrections and new tools

> Status: 🔄 In Progress (P2-1 ✅ done `a91b2b71`, P2-2 ✅ done, P2-3 ✅ done, P2-4 ✅ done — P2-5 ✅ done)
> Scope: Both servers (`tools/rag-dotnet/`, `tools/rag/`)  
> Follow TDD: write failing pipeline-test checks first, then implement.

### Design corrections identified after Phase 1

Phase 1 introduced `POST /config` as a server-side classification layer. After review, this is
the wrong responsibility boundary:

- The **client** (CLI) already has `metadata-rules.yaml` locally — it should classify each
  document itself and pass `doc_kind` + `adr_id` (or the applicable history key) in every
  `POST /ingest` body.
- The **server** should only embed and store. It must never need to infer doc_kind or adr_id
  from path patterns at runtime.
- `POST /config` therefore becomes dead code once the client sends all metadata explicitly.

Phase 1 also deviated from the zip-based CLI design described in Step 11: it used N × single-file
JSON POST instead. Phase 2 restores the zip approach with a proper `manifest.json` contract.

---

### Step P2-1 — Remove `POST /config`; classification via bundled `metadata-rules.yaml` ✅ Done (`a91b2b71`)

The separate `POST /config` endpoint exists because Phase 1's single-file `POST /ingest`
has no way to carry classification rules alongside each document. In the zip-batch design
(P2-2), `metadata-rules.yaml` travels inside the zip — making `POST /config` redundant.

**Remove after P2-2 is implemented:**

- `.NET`: `IngestController.cs` — remove `POST /config` action; remove `RagConfig` injection.
- `Python`: `ingest_routes.py` — remove `POST /config` route; remove `cfg` parameter from
  `build_ingest_routes()`.
- `Python`: `common.py` — remove `set_metadata_rules_override()` and `_metadata_rules_override`
  field; restore `frozen=True`.
- `Python`: `mcp_server.py` — remove `cfg=CFG` from `build_ingest_routes()` call.
- `.NET`: `RagConfig.cs` — remove `SetMetadataRulesOverride()` and `EffectiveMetadataRules`.
- Both: remove the `POST /config` checks from `test_full_pipeline.py` phase 7.

---

### Step P2-2 — ZIP batch ingest endpoint ✅ Done

**Implemented**: `POST /ingest/{collection}/batch` — raw `application/zip` body; returns `202 { batchId, count, operations[] }`. Per-file polling via existing `GET /ingest/{collection}/operations/{opId}`. Python 47/47 unit tests pass; .NET 213/213 unit tests pass. Pipeline test phase 7 extended with 7c/7d batch checks.

**Scope delivered** (simplified relative to original spec — no `manifest.json`, no bundled YAML validation):
- Accepts raw ZIP body; skips directory entries
- Returns one `operationId` per file for independent polling
- `503` when queue cannot fit all files; `400` for invalid or empty ZIP
- Tests: 10 Python + 7 .NET unit tests

**Original TDD spec (below) described a richer design with manifest/config validation — deferred to P2-3+):**

**Original TDD first — add failing phase 8 checks to `test_full_pipeline.py`:**

```python
# Phase 8: ZIP batch ingest
# 1. Build a zip with: config.yaml + metadata-rules.yaml + queries.yaml + 3 synthetic docs
# 2. POST zip to /ingest/{collection}/batch — expect 202 + operationId
# 3. Poll until Completed
# 4. Query each doc via MCP query_docs — verify all 3 are indexed
# 5. Verify completed operation response includes 'manifest' + 'index_stats' fields
#
# Negative cases (separate checks):
# 6. POST zip WITHOUT metadata-rules.yaml → expect 400
# 7. POST zip with metadata-rules.yaml present but doc_kind_rules empty → expect 400
# 8. POST zip with queries.yaml present but named_queries empty → expect 400
# 9. POST zip with queries.yaml referencing a doc_kind not in metadata-rules → expect 400
```

**Zip structure:**

```
batch.zip/
  manifest.json          ← required; lists every file with its metadata
  docs/
    adr/0016/0016-sales-coupons.md
    architecture/bounded-context-map.md
    ...
```

**`manifest.json` schema:**

```json
{
  "collection": "ecommerceapp_docs",
  "created_at": "2025-07-01T12:00:00Z",
  "files": [
    {
      "rel_path": "docs/adr/0016/0016-sales-coupons.md",
      "doc_kind": "adr",
      "history_id": "0016"
    },
    {
      "rel_path": "docs/architecture/bounded-context-map.md",
      "doc_kind": "context",
      "history_id": null
    }
  ]
}
```

`history_id` is always the manifest field name. The server stores it in Qdrant under the
configured `history.field` value (e.g. `adr_id`). This keeps the manifest schema stable
across projects regardless of how each project names its history key.

This manifest is generated by the server after ingest and returned in the completed operation
response. It is NOT uploaded by the client — the client has no manifest before the first ingest.
```

**Zip structure:**

```
batch.zip/
  config.yaml                    ← defines collection, model, chunker, and config_files
  metadata-rules.yaml            ← REQUIRED: doc_kind taxonomy + history.field + adr_id_patterns
  queries.yaml                   ← REQUIRED: named queries (share doc_kind vocabulary with metadata-rules)
  multilingual-glossary.yaml     ← optional (config_files.multilingual_glossary)
  docs/
    adr/0016/0016-sales-coupons.md
    architecture/bounded-context-map.md
    ...
```

The client does **not** include `manifest.json` in the zip — that is a server output, not an
input. The server classifies each doc using the `metadata-rules.yaml` bundled in the zip (same
rules the local ingest uses) and generates the manifest + index-stats on completion.

**`metadata-rules.yaml` and `queries.yaml` are coupled by design:**
Both files share a `doc_kind` vocabulary. `metadata-rules.yaml` defines which `doc_kind` label
each file receives during ingest; `queries.yaml` uses those exact label names as filters in named
queries. Uploading one without the other produces silent failures (named queries return 0 results)
— hence both are required.

**Validation on upload:**

1. Server reads `config.yaml` from the zip root.
2. `metadata-rules.yaml` and `queries.yaml` are **always required** in the zip regardless of
   `config_files` settings — the collection cannot classify or evaluate without them.
   Other `config_files` entries (`multilingual_glossary`, etc.) follow the optional/required flag
   set in `config.yaml`.
3. **Non-empty content validation:**
   - `metadata-rules.yaml` must contain at least one `doc_kind_rules` entry → `400` if empty or
     missing the `doc_kind_rules` key. An empty rules list means all docs get no `doc_kind` and
     all named query filters silently return 0 results.
   - `queries.yaml` must contain at least one entry in `named_queries` → `400` if empty or
     missing the `named_queries` key. An empty list means the eval pipeline has nothing to run.
4. **Cross-file vocabulary validation:** every `doc_kind` value referenced in `queries.yaml`
   named queries must have a matching rule in `metadata-rules.yaml` `doc_kind_rules`. Mismatch →
   `400 Bad Request` listing the unknown doc_kind names. This prevents silent query failures.
5. Proceed to enqueue one `IngestJob` per `.md` file found under paths matching `source.roots`.

**Server endpoint (both servers):**

- `POST /ingest/{collection}/batch` — accepts `multipart/form-data` with a single `file` field
  containing the zip.
- Returns `202 + operationId` immediately (one operation for the whole batch).
- The single `OperationRecord` tracks batch progress: `{ status, processed, total, errors[] }`.
  Callers poll `GET /ingest/{collection}/operations/{opId}` until `status == "Completed"` or
  `"Failed"`.
- When all files in the batch are processed, the completed operation response includes:

```json
{
  "status": "Completed",
  "operationId": "...",
  "processed": 173,
  "total": 173,
  "errors": [],
  "manifest": { ... },         // generated manifest.json (replaces .rag/manifest.json locally)
  "index_stats": { ... }       // generated index-stats content (replaces docs/rag/index-stats.md)
}
```

- Client CLI writes the returned `manifest` to `.rag/manifest.json` and `index_stats` to
  `docs/rag/index-stats.md` — matching paths defined in `config.yaml` under `storage.*`.

**Decision — existing single-file `POST /ingest/{collection}`:**
Keep for backward compatibility until the zip batch endpoint is confirmed stable in the
pipeline test. Remove in a follow-up step (P2-5) once both servers pass phase 8.

---

### Step P2-3 — `get_history(id)` tool — collection-defined history field ✅ Done

**Delivered (additive approach):** `get_history(id)` added alongside `get_adr_history`. `get_adr_history` removed in P2-5.

- **Python** (`tools/rag/`): `field_filter` param in `QueryEngine.search()`; `get_history` MCP tool in `mcp_server.py`; reads `history_field` from `__config__` point, defaults to `"adr_id"`.
- **.NET** (`tools/rag-dotnet/`): `HistoryFieldFilter` in `SearchOptions`; `HistoryField` in `RagConfigPayload`; `historyFieldFilter` param in `QdrantStore.SearchAsync()`; `GetHistory` MCP tool in `RagTools.cs`.
- **Tests**: 4 E2E tests in `RagE2ETests.cs` (217/217 green); 4 pipeline phase 9 checks in `test_full_pipeline.py`.

**Original TDD spec (kept for reference):**

```python
# 1. Ingest a synthetic doc with a custom history key in the body
#    e.g. body = { rel_path, content, doc_kind: "adr", adr_id: "TEST-42" }
# 2. Call get_history via MCP SSE with id="TEST-42"
# 3. Assert chunks returned; assert adr_id field in each chunk == "TEST-42"
# 4. Call get_adr_history with the same id — assert it no longer exists (tool removed)
```

**`metadata-rules.yaml` addition (both projects):**

```yaml
history:
  field: adr_id   # Qdrant payload field used to group chunks for get_history()
                  # Set to the field name that identifies related doc versions.
                  # Another project might use "rfc_id", "decision_id", etc.
```

**At ingest time (both servers):**

- Parse `history.field` from the zip's embedded `metadata-rules.yaml` (or standalone config).
- Store it in the collection config point in Qdrant (`doc_kind: "__config__"`, `history_field`
  key in payload).
- The field can be updated by re-ingesting; tools always read it fresh from the config point.

**`get_history(id)` — new MCP tool (both servers):**

```python
# Python
@tool
async def get_history(id: str) -> str:
    """Return all chunks for a document group identified by 'id'.
    The grouping field (e.g. adr_id, rfc_id) is defined in the collection config.
    Use this to retrieve the full text of an ADR, RFC, or any versioned decision record."""
    field = await store.fetch_history_field(collection)   # reads __config__ point
    results = await store.search(collection, filter={field: id}, top_k=50)
    ...
```

```csharp
// .NET
[McpServerTool, Description("Return all chunks for a document group identified by 'id'...")]
public async Task<string> GetHistory(
    [Description("ID value to look up (e.g. '0016', 'RFC-003'). " +
                 "Matched against the collection's configured history field.")] string id,
    ...)
```

**Removal:**
- Remove `get_adr_history` from both servers after `get_history` passes the phase 8 checks.
- Update `copilot-instructions.md` tool routing table (section 12) to reference `get_history`.
- Update `rag.instructions.md` tool table.

---

### Step P2-4 — Pipeline test coverage for Phase 2

After steps P2-1 through P2-3 all pass, the pipeline test should show:

| Phase | Checks | Description |
|---|---|---|
| 7 | 6 (down from 8) | Hosted ingest via single-file POST — no POST /config checks |
| 8 | 10 | ZIP batch ingest: upload, poll, MCP verify × 3 docs + manifest return |
| 9 | 6 | `get_history` tool: 3 docs queried by history id × 2 servers |

Total target: **≥ 50 checks**, all green.

---

### Step P2-5 — Remove legacy endpoints ✅ Done

Removed `POST /ingest/{collection}` (single-file JSON) from both servers and removed `get_adr_history` (disk-based) from both servers. All tests updated to use batch ingest and `get_history` instead.

When `--remote` is NOT provided: existing local ingest behavior unchanged.

Additional operation management commands:
```
# Check status of a specific operation
rag-ingest --remote http://... ops status <operationId>

# List recent operations (calls GET /ingest/{collection}/operations)
rag-ingest --remote http://... ops list [--collection ecommerceapp] [--status processing]
```

Output table for `ops list`:
```
OperationId  Collection      Status      Started              Files  Chunks
3f7a2c       ecommerceapp    completed   2026-05-21 10:00     87     1842
a1b2c3       ecommerceapp    processing  2026-05-21 10:42     34     —
```

---

## Implementation Plan — Phase 2: Python Server

Python changes mirror the .NET plan. Key differences:

| Area | .NET approach | Python approach |
|---|---|---|
| Background worker | `Channel<T>` + `BackgroundService` | `asyncio.Queue` + background `asyncio.Task` |
| API key middleware | ASP.NET middleware on `/ingest/*` | Starlette middleware or route-level check |
| Ingest controller | `[ApiController]` | Starlette `Route` + `handle_ingest` function |
| Operation store | `OperationStore` wrapping Qdrant | `operation_store.py` wrapping `qdrant_client` |
| CLI remote mode | `--remote` flag in `Program.cs` | `--remote` flag in `ingest.py` |
| Session project | `RagSession` scoped service | `session_project` context var in Starlette request |

Python phase starts after Step 10 of the .NET phase is complete and verified.

---

## mcp.json updates (both phases)

After the server is deployed remotely, add entries like:

```json
"ecommerceapp-rag-remote": {
  "type": "http",
  "url": "http://rag.internal:3001/?project=ecommerceapp"
}
```

Local Docker and stdio entries remain unchanged as fallback.

---

## Dependency order

```
Step 0 (IDocumentStore interface)
  ↓
Step 1 (DocKind schema + DeterministicId)
  ↓
Step 2 (content_id + ContentDocument in QdrantDocumentStore)
  ↓
Step 3 (config/glossary/queries as structured JSON)
  ↓
Step 4 (caching: QueryCache + CachedDocumentStore)
  ↓
Step 5 (worker) → Step 6 (op store, adds ListAsync) → Step 7 (controller: upload + status + list + /admin/stats)
                                                            ↓
Step 8 (auth middleware — applied to /ingest/* only)
Step 9 (session project) ──────────────────── Step 10 (read_docs via IDocumentStore.FetchContentAsync)
Step 11 (CLI remote mode + ops list) — independent from Step 5 onward
```

Steps 0–3 are foundational and must be done first (schema and interface are the bedrock).  
Step 4 (caching) after Step 3, before worker (worker triggers cache invalidation).  
Steps 5–7 form the ingest pipeline.  
Steps 8–10 are independent improvements.  
Step 11 can be done in parallel from Step 5 onward.

---

## Edge cases to handle

| Edge case | Handling |
|---|---|
| Upload with no config (subsequent re-upload) | Use last stored config from Qdrant; 400 if no stored config exists yet |
| Zip contains binary files | Skip (filter to text/markdown extensions only) |
| File > 10 MB | Reject with 413 during upload; log warning |
| Empty collection (no data ingested yet) | `query_docs` returns "No results found" — no crash |
| `__ops__` collection missing on first query | Auto-create on `OperationStore` initialization |
| Concurrent uploads to same collection | Allowed; chunk IDs are deterministic, so parallel upserts are safe |
| Server restart mid-ingest | Operation stays `"processing"`; caller detects stale by `created_at > 1h` |
| `project` param missing from SSE URL | Falls back to `RAG_COLLECTION` env var |
| `RAG_API_KEY` env var not set | Server starts; write endpoints return 401; warning logged at startup |
| Re-upload of same files (idempotent) | Safe — upsert by deterministic ID overwrites identical data |
| Qdrant payload > 500 MB | Switch to `LocalFileDocumentStore` or `BlobDocumentStore` (IDocumentStore swap, zero tool changes) |
| Single doc > 500 KB | Log warning during ingest; consider splitting or switching store impl |
| `queries.yaml` missing from upload | Skip silently; existing named eval queries in Qdrant unchanged |
| Cache invalidated but new ingest fails mid-way | Content cache TTL (2 min) ensures stale data is not served long |
| `RAG_*_CACHE_TTL_SECONDS=0` for a cache | That specific cache is disabled entirely — data always fetched from Qdrant |

---

## Acceptance criteria

### Ingest API
- [ ] `POST /ingest/ecommerceapp` with valid API key returns 202 with `operationId`
- [ ] `GET /ingest/ecommerceapp/operations/{id}` returns `completed` after background processing
- [ ] `GET /ingest/ecommerceapp/operations` returns list of recent operations
- [ ] `POST /ingest/*` returns 401 without `X-Api-Key` header
- [ ] MCP tool endpoints do NOT require `X-Api-Key`
- [ ] `GET /admin/stats` returns collection names, chunk counts, content payload MB, store impl name
- [ ] Expired operations (> 5 days) are removed by lazy sweep

### Storage and indexing
- [ ] `query_docs` works against a collection ingested via the upload endpoint (no volume mount)
- [ ] `read_docs` returns full file content from Qdrant payload (not from disk)
- [ ] Every chunk payload includes `chunk_index` and `content_id`
- [ ] Config, glossary, rules, queries stored as structured JSON (not raw YAML text)
- [ ] `queries.yaml` is ingested and stored per collection
- [ ] All tools use `IDocumentStore` — no direct Qdrant calls in tools code

### Caching
- [ ] Config and glossary caches are invalidated immediately after successful ingest
- [ ] Content cache TTL is 2 min; ADR list cache is 1 hour; embedding cache is 10 min
- [ ] Setting `RAG_CONTENT_CACHE_TTL_SECONDS=0` disables content cache
- [ ] `read_docs` returns content from cache on second call within 2 min

### Backward compatibility
- [ ] MCP SSE connection with `?project=ecommerceapp` routes all tool calls to that collection
- [ ] Local Docker stdio mode (`ecommerceapp-rag-dotnet-docker`) still works unchanged
- [ ] Local `dotnet run` stdio mode (`ecommerceapp-rag-dotnet`) still works unchanged
- [ ] `RagTools.Ingest --remote http://...` successfully uploads and polls to completion
- [ ] `rag-ingest ops list` renders a table of recent operations for a collection
