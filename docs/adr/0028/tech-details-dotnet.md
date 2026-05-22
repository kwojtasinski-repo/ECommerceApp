# ADR-0028: Technical Implementation Details — .NET

> This file documents the implementation specifics of ADR-0028 for the .NET RAG server
> (`tools/rag-dotnet/`). Non-technical context and rationale live in the main ADR.

---

## Component Overview

```
RagTools.Core/          ← domain types, interfaces, background services
  IDocumentStore        ← single abstraction for all storage (chunks, content, config)
  QdrantDocumentStore   ← Qdrant implementation
  CachedDocumentStore   ← decorator: QueryCache wraps QdrantDocumentStore
  QueryCache            ← bounded LRU in-memory cache (max 512 entries, per-entry TTL)
  IngestChannel         ← bounded Channel<IngestJob> (capacity 1000)
  IngestWorker          ← BackgroundService: drains IngestChannel
  OperationStore        ← ConcurrentDictionary<string, IngestOperationResult>; 1h retention
  RagSession            ← active collection for the current SSE session (or stdio singleton)
  IngestJob             ← POCO: OperationId, Collection, RelPath, Content, DocKind?, EnqueuedAt
  IngestOperationResult ← sealed record: OperationId, Status, EnqueuedAt, StartedAt?, CompletedAt?, ChunkCount?, ErrorMessage?

RagTools.Mcp/
  Controllers/IngestController   ← POST /ingest/{collection}, GET /ingest/…/operations/{id}
  Middleware/ApiKeyMiddleware     ← guards /ingest/* and /admin/* (X-Api-Key header)
  Middleware/RagSessionMiddleware ← reads ?project= → session.SetCollection(project)
  Tools/RagTools                 ← MCP tools (query_docs, read_docs, …)

RagTools.Ingest/        ← CLI: local ingest or --remote HTTP push
```

---

## Data Model

### Chunk point (vector search hit)

```json
{
  "vector": [...1024 floats...],
  "payload": {
    "doc_kind":    "chunk",
    "relPath":     "docs/adr/0016/0016-coupons.md",
    "chunk_index": 4,
    "text":        "...",
    "content_id":  "a3f9c1b2-..."
  }
}
```

### Content point (full file, no real vector)

```json
{
  "doc_kind":    "full_content",
  "path":        "docs/adr/0016/0016-coupons.md",
  "title":       "ADR-0016: Coupon limits",
  "bc":          "Coupons",
  "doc_type":    "adr",
  "content":     "# ADR-0016...",
  "ingested_at": "2026-05-21T10:00:00Z",
  "metadata":    {}
}
```

C# record:
```csharp
public sealed record ContentDocument(
    string RelPath, string DocKind, string? Bc, string? Title,
    string Content, DateTimeOffset IngestedAt,
    IDictionary<string, string>? Metadata = null);
```

### Config point (stored as structured JSON, parsed once at upload)

```json
{
  "doc_kind":       "__config__",
  "schema_version": 1,
  "chunker":        { "max_tokens": 512, "overlap": 64 },
  "weights":        { "adr": 1.2, "context": 1.15 },
  "score_threshold": 0.35,
  "fetch_k": 20
}
```

### Reserved `doc_kind` values

```csharp
public static class DocKind
{
    public const string Chunk       = "chunk";
    public const string FullContent = "full_content";
    public const string Config      = "__config__";
    public const string Glossary    = "__glossary__";
    public const string Rules       = "__rules__";
    public const string Queries     = "__queries__";
    public const string Operation   = "__op__";
}
```

All search queries exclude `doc_kind` values starting with `__` and `"full_content"`.

---

## Deterministic ID Scheme (UUID v3 / MD5)

```
Chunk point ID  = UUID(MD5("{collection}:{relPath}:{chunk_index}"))
Content point ID = UUID(MD5("{collection}:{relPath}"))
```

- `content_id` in every chunk payload = the content point ID for that file.
- After a vector search, `read_docs` collects `content_id` values and calls
  `qdrant.GetPointsAsync([id1, id2, ...])` — one O(1) batch fetch, no second search.
- Context neighbors are computed: `UUID(MD5("{collection}:{relPath}:{i}"))` for `i` in `[chunkIndex-N, chunkIndex+N]`.
  Missing IDs (edges of file) come back null and are silently ignored.

---

## IDocumentStore

All ingest and query code goes through this interface. Nothing calls Qdrant directly.

```csharp
public interface IDocumentStore
{
    // Ingest
    Task UpsertChunksAsync(string collection, IEnumerable<ChunkPoint> chunks, CancellationToken ct);
    Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct);
    Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct);
    Task StoreGlossaryAsync(string collection, string[] terms, CancellationToken ct);
    Task DeleteChunksAsync(string collection, string relPath, CancellationToken ct);
    Task RecreateCollectionAsync(string collection, int vectorSize, CancellationToken ct);

    // Query
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string collection, float[] vector, SearchOptions opts, CancellationToken ct);
    Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct);
    Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct);
    Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct);
}
```

Registered in DI as `CachedDocumentStore` (decorator) wrapping `QdrantDocumentStore`:
```csharp
services.AddSingleton<IDocumentStore>(_ =>
    new CachedDocumentStore(new QdrantDocumentStore(qdrantUrl), new QueryCache()));
```

---

## Async Ingest Pipeline

### Channel + Worker

```
HTTP POST /ingest/{collection}
  └─► IngestController.IngestAsync()
       ├─ returns 202 + Location header immediately
       ├─ OperationStore.MarkQueued(opId, ...)
       └─ IngestChannel.TryWrite(job)   [503 if channel full]

IngestChannel: Channel<IngestJob>  capacity=1000, SingleReader=true
IngestWorker:  BackgroundService
  └─ per job: detect kind, chunk, delete old points, embed (batched), upsert, StoreDocumentAsync, MarkCompleted/MarkFailed
```

### IngestJob

```csharp
public class IngestJob
{
    public string OperationId { get; init; }
    public string Collection  { get; init; }
    public string RelPath     { get; init; }
    public string Content     { get; init; }
    public string? DocKind    { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; }
}
```

### OperationStore

In-memory `ConcurrentDictionary<string, IngestOperationResult>`. Entries expire after 1 hour
(checked lazily on every `Get` call). State machine:

```
MarkQueued()    → Status = Queued
MarkProcessing() → Status = Processing  (creates entry even if not previously queued)
MarkCompleted() → Status = Completed, ChunkCount set
MarkFailed()    → Status = Failed, ErrorMessage set
```

---

## HTTP API Contract

### Upload (one file per request in current implementation)

```
POST /ingest/{collection}
X-Api-Key: <key>
Content-Type: application/json

{ "relPath": "docs/concepts/caching.md", "content": "...", "docKind": null }

202 Accepted
Location: /ingest/{collection}/operations/{opId}
{ "operationId": "...", "collection": "...", "relPath": "...", "statusUrl": "..." }

503 Service Unavailable   ← channel full (retry after a few seconds)
401 Unauthorized          ← missing or wrong API key
```

### Status

```
GET /ingest/{collection}/operations/{opId}
X-Api-Key: <key>

200 OK   { "operationId": "...", "status": "completed", "chunkCount": 12 }
404      ← operation unknown or expired (> 1 hour)
```

### List operations

```
GET /ingest/{collection}/operations
X-Api-Key: <key>

200 OK  [ { "operationId": "...", "status": "...", ... }, ... ]
```

### Admin stats

```
GET /admin/stats
X-Api-Key: <key>

200 OK  { "pendingCount": 3, "retentionPeriod": "01:00:00", "operations": [...] }
```

---

## API Key Middleware

Guards `/ingest/*` and `/admin/*`. MCP tool endpoints (`/`, `/sse`) are not guarded.

```csharp
// Middleware/ApiKeyMiddleware.cs
// Reads X-Api-Key header, compares to RAG_API_KEY env var.
// If env var unset: logs warning + allows request (dev mode).
// If env var set and header missing/wrong: 401 JSON response.
```

---

## RagSession

Binds the active collection to the current SSE session (or to the process for stdio).

```csharp
public sealed class RagSession
{
    public string Collection { get; private set; }

    public RagSession(RagConfig cfg) => Collection = cfg.Collection;
    public void SetCollection(string name) => Collection = name;
}
```

- **SSE mode**: registered as `Scoped` — one `RagSession` per SSE connection.
  `RagSessionMiddleware` reads `?project=` from the query string and calls `SetCollection`.
- **stdio mode**: registered as `Singleton` — `RagSession` holds `cfg.Collection` for the process lifetime.

---

## Caching Strategy

`CachedDocumentStore` wraps `QdrantDocumentStore`. Cache is a `QueryCache` instance
(bounded LRU, max 512 entries).

| Cache | Key prefix | TTL | Invalidation |
|---|---|---|---|
| Search results | `search:{collection}:...` | 5 min | On ingest (`InvalidatePrefix`) |
| Content (full file) | `content:{collection}:{relPath}` | 15 min | On ingest |
| Config | `config:{collection}` | 30 min | On ingest |
| ADR list | `adrs:{collection}` | 10 min | On ingest |

`QueryCache.GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl)` — thread-safe
via `ConcurrentDictionary`. FIFO eviction when capacity (512) is reached.

---

## Ingest CLI — Remote Mode

`RagTools.Ingest` supports a `--remote <url>` flag:

```sh
dotnet run --project tools/rag-dotnet/src/RagTools.Ingest -- --remote http://rag.internal:3001
```

- Reads `config.yaml` for the collection name.
- Iterates workspace files matching configured globs.
- Computes SHA-256 hash; skips unchanged files (manifest).
- POSTs each file to `POST /ingest/{collection}` with `X-Api-Key` from `RAG_API_KEY` env.
- Retries once on `503 Service Unavailable`.
- Updates manifest (`manifest.Update(relPath, hash, 0); manifest.Save()`).

---

## DI Wiring (SSE mode)

```csharp
webBuilder.Services
    .AddControllers().Services
    .AddSingleton(cfg)
    .AddSingleton(_ => OnnxEmbedder.Load(modelDir))
    .AddSingleton<IDocumentStore>(_ =>
        new CachedDocumentStore(new QdrantDocumentStore(qdrantUrl), new QueryCache()))
    .AddSingleton<IngestChannel>()
    .AddSingleton<OperationStore>()
    .AddHostedService<IngestWorker>()
    .AddScoped<RagSession>()
    .AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

// Middleware order:
// ApiKeyMiddleware → RagSessionMiddleware → MapControllers → MapMcp("/")
```

---

## Conformance Checklist

### Ingest API
- [ ] `POST /ingest/{collection}` returns 202 + Location header; 503 when queue full
- [ ] `GET /ingest/{collection}/operations/{id}` returns 200 or 404
- [ ] All `/ingest/*` and `/admin/*` endpoints require `X-Api-Key`
- [ ] MCP endpoints do NOT require `X-Api-Key`
- [ ] `RAG_API_KEY` unset → warn at startup + allow (dev mode)

### Storage
- [ ] Chunk IDs: `UUID(MD5("{collection}:{relPath}:{chunk_index}"))`
- [ ] Content point IDs: `UUID(MD5("{collection}:{relPath}"))`
- [ ] Every chunk payload contains `content_id` matching its content point
- [ ] All search queries exclude `doc_kind` prefixed with `__` and `"full_content"`
- [ ] `IDocumentStore` is the only path to Qdrant — no direct SDK calls in tools or worker

### Caching
- [ ] `CachedDocumentStore` invalidates by prefix on ingest
- [ ] `QueryCache` max capacity 512 entries, FIFO eviction
- [ ] Config TTL 30 min; Content TTL 15 min; Search TTL 5 min; ADR list TTL 10 min

### Session
- [ ] SSE: `RagSession` is `Scoped`; `RagSessionMiddleware` reads `?project=`
- [ ] stdio: `RagSession` is `Singleton`; uses `cfg.Collection`
