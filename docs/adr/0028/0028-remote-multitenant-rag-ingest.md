# ADR-0028: Remote-Capable Multi-Tenant RAG Server with Async Ingest API

## Status
Proposed

## Date
2026-05-21

## Context

ADR-0027 established a local RAG pipeline over MCP (stdio + SSE transports). In that design the
RAG server reads documentation files directly from the mounted workspace volume at query time,
and the collection name is fixed per server instance via the `RAG_COLLECTION` environment variable.

This creates two hard blockers for a shared-team deployment:

1. **Filesystem coupling at query time**: the running server must have the workspace files
   accessible via a volume mount. When the server is deployed remotely (e.g. a team VM, a
   Kubernetes pod, a cloud VM), the developer's local docs are not available to the server at
   runtime.

2. **One collection per server instance**: to serve two projects you must run two containers,
   each loading a full 470 MB ONNX model into memory. This is wasteful and operationally
   awkward for teams that maintain multiple repos.

### Additional requirements surfaced during design

- Developers need to push docs from their local machines to the remote server without giving
  the server access to their filesystems.
- Large documentation sets (hundreds of files) can take 10–30 s to ingest; the ingest call
  must be non-blocking.
- Config files (`config.yaml`, `multilingual-glossary.yaml`, `metadata-rules.yaml`) travel with
  the docs and must be versioned per collection — not baked into the server image.
- The server must be the sole source of truth for a collection; Qdrant is already the vector
  store, so extending it to hold config and operation metadata avoids adding a second dependency.
- The team is small (2–5 people), trusts each other, and does not need per-user identity.
  A shared API key is sufficient authentication.

---

## Decision

### 1. Collection identity = app/project name (derived from `config.yaml`)

The `collection` field in `config.yaml` becomes the canonical identifier for a project.
It is used as the Qdrant collection name and as the MCP project selector. Example:

```yaml
collection: ecommerceapp         # ← becomes the project/collection ID
```

**Rationale**: The manifest file already travels with the docs and is uploaded as part of
every ingest job. Deriving the collection name from it makes the identity deterministic and
self-describing without additional registration steps.

---

### 2. Project selection via URL query param at SSE connection time

MCP SSE clients declare which collection they belong to via a query parameter on the MCP URL:

```json
"ecommerceapp-rag": {
  "type": "http",
  "url": "http://rag.internal:3001/?project=ecommerceapp"
}
```

The server reads `project` from the query string during the SSE/Streamable-HTTP handshake and
binds it to the session. All tool calls in that session (`query_docs`, `read_docs`, etc.) use
that collection without the caller needing to pass `project` explicitly.

An environment variable `RAG_COLLECTION` remains as the server-level default for stdio mode
and for SSE clients that omit the query param, preserving full backward compatibility.

**Rationale**: Cleaner than passing `project` on every tool call; avoids prompt-engineering
the AI to always include the parameter; and allows one mcp.json entry per project without
duplicating server infrastructure.

---

### 3. Async ingest API

Docs are uploaded by developers from their local machines to the running server.
The server processes them in the background and returns an operation ID immediately.

#### Upload endpoint

```
POST /ingest/{collection}
Authorization: X-Api-Key <server-key>
Content-Type: multipart/form-data

Parts:
  docs[]                      — one or more text/markdown files (or a single .zip)
  config.yaml                 — chunker, weights, score thresholds  (optional on subsequent uploads)
  multilingual-glossary.yaml  (optional)
  metadata-rules.yaml         (optional)
  queries.yaml                (optional — named eval queries for this collection)

Response: 202 Accepted
{
  "operationId": "3f7a2c",
  "collection": "ecommerceapp",
  "filesReceived": 87,
  "status": "queued"
}
```

#### Status endpoint

```
GET /ingest/{collection}/operations/{operationId}
Authorization: X-Api-Key <server-key>

Response: 200 OK
{ "operationId": "3f7a2c", "status": "processing", "chunked": 42, "embedded": 30, "total": 87 }
{ "operationId": "3f7a2c", "status": "completed", "chunksUpserted": 1842, "durationMs": 14200 }
{ "operationId": "3f7a2c", "status": "failed",    "error": "..." }
```

#### Background worker

A `Channel<IngestJob>` (bounded capacity) decouples upload from processing. One background
`IHostedService` drains the channel, runs chunking + embedding, and upserts to Qdrant.

Ingest is **idempotent**: each chunk point uses a deterministic ID composed of
`{collection}:{file_path}:{chunk_index}` so re-uploading the same file overwrites existing
chunks without creating duplicates.

---

### 4. Document storage abstraction: `IDocumentStore`

All ingest and query operations go through a single interface — tools and the ingest worker
never call Qdrant directly:

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

The implementation is selected at startup via DI — callers never reference Qdrant directly.
Swapping to a different backend (Redis Vectors, Elasticsearch, blob store) is a single
DI registration change with zero changes to tools or ingest code.

| Implementation | Content | Vectors | When |
|---|---|---|---|
| `QdrantDocumentStore` | Qdrant payload | Qdrant | v1 (default) |
| `LocalFileDocumentStore` | Server disk `/data/` | Qdrant | If payload > 500 MB |
| `BlobDocumentStore` | S3 / Azure Blob | Qdrant or SaaS vector DB | At scale |

---

### 5. Structured document format — no raw strings

All stored data uses structured JSON payloads, not raw YAML or plain text strings.
This eliminates YAML parsing at read time, makes data inspectable in Qdrant dashboard,
and provides a versioned schema for future migrations.

#### `ContentDocument` (full file point)

```json
{
  "doc_kind": "full_content",
  "path": "docs/adr/0016/0016-coupons.md",
  "title": "ADR-0016: Coupon limits",
  "bc": "Coupons",
  "doc_type": "adr",
  "content": "# ADR-0016...",
  "ingested_at": "2026-05-21T10:00:00Z",
  "metadata": {}    ← extensible bag for future fields
}
```

C# record:
```csharp
public sealed record ContentDocument(
    string RelPath, string DocKind, string? Bc, string? Title,
    string Content, DateTimeOffset IngestedAt,
    IDictionary<string, string>? Metadata = null);
```

#### Config / glossary points (parsed once at upload, stored as JSON)

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

`config.yaml` is parsed **once at upload time**. A YAML format error causes a `400 Bad Request`
with a clear message — caught before data is stored. The server reads back the structured JSON
at startup; no YAML parser is needed at runtime.

#### Reserved `doc_kind` values

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

All search queries exclude `doc_kind` values prefixed with `__` and `full_content`.

---

### 6. Chunk → content link and neighbor computation

#### `content_id` in every chunk payload

Each chunk point stores the ID of its corresponding full-content point:

```json
{
  "vector": [...embedding...],
  "payload": {
    "doc_kind": "chunk",
    "relPath": "docs/adr/0016/...",
    "chunk_index": 4,
    "text": "...",
    "content_id": "a3f9c1b2-..."    ← deterministic UUID, available free in every search result
  }
}
```

`content_id` = `UUID(MD5("{collection}:{relPath}"))` — deterministic, computed at ingest time.
After a vector search, `read_docs` collects the `content_id` values from the hit chunks and
calls `qdrant.GetPointsAsync([id1, id2, ...])` — one direct O(1) batch fetch, no second search,
no filter scan.

#### Context expansion via computed chunk neighbors

Chunk IDs are also deterministic: `UUID(MD5("{collection}:{relPath}:{chunk_index}"))`.
Neighbor chunks (±N) can be fetched without any stored prev/next pointer:

```csharp
// fetch ±2 chunks around the matched chunk
var neighborIds = Enumerable.Range(chunkIndex - 2, 5)
    .Select(i => ComputeChunkId(collection, relPath, i))
    .ToList();
var neighbors = await qdrant.GetPointsAsync(neighborIds);  // missing IDs → null, ignored
```

One batch call returns the full context window. Chunk index is stored in the chunk payload
(already present in current ingest) so neighbors can be computed from any search result.

---

### 7. Caching layer — six caches, all configurable via env

All caches are invalidated for a collection when `POST /ingest/{collection}` completes.
TTLs are configured via environment variables (set to `0` to disable a specific cache).

| Cache | Key | TTL | Env var | Notes |
|---|---|---|---|---|
| Config | `{collection}` | Until ingest (+ safety net) | `RAG_CONFIG_CACHE_TTL_SECONDS` | Safety net default: 3600 s |
| Glossary | `{collection}` | Until ingest (+ safety net) | `RAG_GLOSSARY_CACHE_TTL_SECONDS` | Safety net default: 3600 s |
| Document content | `{collection}:{relPath}` | 2 min | `RAG_CONTENT_CACHE_TTL_SECONDS` | Default: 120 s |
| ADR list | `{collection}` | 1 hour | `RAG_ADR_LIST_CACHE_TTL_SECONDS` | Invalidated on ingest |
| Query embedding | `SHA256(expanded_text)` | 10 min | `RAG_EMBED_CACHE_TTL_SECONDS` | Saves 10–50 ms ONNX inference |
| Chunk context | `{collection}:{chunk_id}` | 2 min | Reuses `RAG_CONTENT_CACHE_TTL_SECONDS` | |

**Not cached**: search results (near-zero hit rate — queries are unique; ONNX step already
covered by embedding cache).

Config and glossary caches are primarily **event-driven** (cleared on ingest). The TTL
environment variable acts as a safety net only — e.g., if a server crash prevents the
invalidation signal from reaching the cache.

All caches share a single `IMemoryCache` with distinct key prefixes. The `QueryCache`
service holds embedding and ADR list caches. A `CachedDocumentStore` decorator wraps
`IDocumentStore` for content and chunk context caches.

---

### 8. Config and glossary stored in Qdrant per collection

On upload, config files are stored as special Qdrant points with zero vectors:

| Point ID | `doc_kind` | Contents |
|---|---|---|
| `__config__:v1` | `"__config__"` | parsed + serialized as structured JSON |
| `__glossary__:v1` | `"__glossary__"` | parsed + serialized as structured JSON |
| `__rules__:v1` | `"__rules__"` | parsed + serialized as structured JSON |
| `__queries__:v1` | `"__queries__"` | parsed + serialized as structured JSON |

Parsing happens at upload time. YAML errors produce `400 Bad Request` immediately.
Server reads structured JSON at startup — no YAML parser needed at runtime.
On server startup (or on first request to a collection), config is loaded from Qdrant,
cached, and refreshed only when new content is uploaded.

---

### 9. Operation status in Qdrant, 5-day TTL, lazy cleanup

Operation state is persisted as a Qdrant point in a dedicated `__ops__` collection
(not inside the project collection):

```json
{
  "id": "3f7a2c",
  "payload": {
    "doc_kind": "__op__",
    "collection": "ecommerceapp",
    "status": "completed",
    "created_at": "2026-05-21T10:00:00Z",
    "expires_at": "2026-05-26T10:00:00Z",
    "chunksUpserted": 1842
  }
}
```

Qdrant has no native TTL. Expired operations are cleaned up lazily: any request to
`GET /ingest/{collection}/operations/{id}` also triggers a background sweep that deletes
points where `expires_at < now`. A scheduled sweep also runs every 6 hours.

If the server crashes mid-ingest, the operation remains in `"processing"` status indefinitely.
Callers can detect a stale operation by checking `created_at` — if `processing` for more than
1 hour, it is considered failed. A future amendment may add a `watchdog_timeout_minutes` field.

---

### 10. API key authentication

A single shared API key protects all write endpoints (`POST /ingest/*`, `GET /ingest/*/operations/*`).
MCP read endpoints (tool calls via SSE/stdio) are not protected — they are assumed to be
accessible only within the team network.

The key is passed as an HTTP header:
```
X-Api-Key: <value>
```

The key is configured via the `RAG_API_KEY` environment variable. If the variable is unset,
write endpoints reject all requests with `401 Unauthorized` and log a warning at startup.

**Rationale**: Small team on a trusted internal network. JWT/OAuth adds complexity without
proportional security benefit at this scale. The key can be rotated by restarting the server
with a new env var value.

---

### 11. Ingest CLI — one per server flavor

| CLI | Language | How it works |
|---|---|---|
| `RagTools.Ingest` (extended) | .NET | `--remote http://rag.internal:3001` flag — zips workspace, POSTs to `/ingest/{collection}`, polls status |
| `tools/rag/ingest.py` (extended) | Python | `--remote http://...` flag — same zip + POST + poll flow |

Both CLIs derive `collection` from `config.yaml` in the workspace — no separate flag needed.
Both CLIs read `RAG_API_KEY` from env or accept `--api-key` flag.

The CLI also supports operation management commands:
```
# Check a specific operation
rag-ingest --remote http://... ops status <operationId>

# List recent operations for a collection
rag-ingest --remote http://... ops list [--collection ecommerceapp] [--status processing]
```

The `ops list` command calls `GET /ingest/{collection}/operations` and renders a table:
```
OperationId  Collection      Status      Started              Files  Chunks
3f7a2c       ecommerceapp    completed   2026-05-21 10:00     87     1842
a1b2c3       ecommerceapp    processing  2026-05-21 10:42     34     —
```

---

## Consequences

### Positive

- Server is fully stateless with respect to docs — can restart, migrate, or scale without data loss.
- One ONNX model instance shared across all collections on the same server (significant memory saving for multi-project use).
- Ingest is idempotent — safe to re-run from CI or on every docs change.
- Single dependency at runtime: Qdrant. No blob store, no Redis, no shared volume.
- `IDocumentStore` abstraction allows backend swap (Qdrant → blob, Redis, etc.) without touching tools or ingest code.
- `content_id` in chunk payloads enables O(1) batch content fetch after vector search — no second search needed.
- Six configurable caches (all via env vars) cover every hot read path: config, glossary, content, ADR list, embeddings, chunk context.
- Full backward compatibility: stdio mode with `RAG_COLLECTION` env var unchanged.

### Negative

- Larger Qdrant payloads: full content + config stored as JSON in addition to chunk embeddings.
  (For ~150 files × 10 KB average: ~1.5 MB payload overhead — negligible; threshold to consider external store is ~500 MB total payload.)
- Ingest step is now required before the server can answer queries for a new collection.
  Fresh server + fresh Qdrant = no results until first upload completes.
- Server crash mid-ingest leaves stale operation status (watchdog mitigates).
- API key is a shared secret; rotation requires server restart.

### Risks & mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| Large file causes OOM during embedding | Low–Medium | Stream-chunk large files; enforce per-upload file size limit (e.g. 10 MB/file) |
| Stale `processing` operation after crash | Low | `created_at` age check by caller; future watchdog field |
| API key leak | Low | Treat as internal infra secret; rotate on suspicion via env var |
| Qdrant used as config store (unusual) | Low | Config points use reserved `doc_kind` prefixed with `__`; excluded from all search queries |
| Re-upload wipes valid data on partial failure | Low | Ingest is chunk-level upsert, not collection drop/recreate; partial re-upload is safe |

---

## Alternatives considered

### A. Filesystem volume mount (current ADR-0027 design)
Rejected for remote deployment: the server cannot access the developer's local workspace.
Works well for local Docker; remains unchanged for that use case.

### B. Server-side blob volume (`/data/{collection}/`)
Rejected: adds a persistent volume dependency. Moves the problem from "filesystem coupling"
to "volume provisioning". Qdrant as the single store is simpler operationally.

### C. Full content via chunk stitching only (no content points)
Rejected: stitching N chunks may reorder text, lose section context across chunk boundaries,
and cannot guarantee complete coverage of large files. The extra storage cost of content
points is negligible.

### G. Direct Qdrant calls instead of `IDocumentStore`
Rejected: couples all tools and the ingest worker to Qdrant's SDK. Swapping the backend
would require changes across the entire codebase. The interface adds a 10-line abstraction
that pays off on the first backend switch or unit test.

### H. Cache search results
Rejected: cache key must include a hash of the query embedding vector. Different phrasings
produce different vectors → near-zero cache hit rate in real use. The embedding cache already
covers the ONNX inference step (the largest CPU cost). Qdrant vector scan (20–50 ms) is
acceptable without a result cache at team scale.

### I. Store prev/next chunk IDs in payload for context expansion
Rejected: unnecessary if chunk IDs are deterministic.
`UUID(MD5("{collection}:{relPath}:{chunk_index}"))` allows the caller to compute any
neighbor ID without stored pointers. One batch `GetPointsAsync` call fetches all neighbors.

### D. Per-call `project` parameter on every tool call
Rejected: verbose; forces the AI assistant to always include the parameter; risks the parameter
being omitted when Copilot generates the tool call. URL-based session binding is cleaner.

### E. JWT / OAuth for auth
Rejected: disproportionate complexity for a 2–5 person team on a trusted internal network.
API key is sufficient and simpler to operate.

### F. Redis / Postgres for operation status
Rejected: introduces a second runtime dependency. Qdrant with a dedicated `__ops__` collection
achieves the same goal with the existing infrastructure.

---

## Migration plan

### Existing local setups (stdio, Docker volume mount)

No changes required. The `RAG_COLLECTION` env var, volume mount, and stdio transport
continue to work exactly as before (ADR-0027 design). This ADR adds new endpoints and a new
ingest mode alongside the existing one — it does not remove or alter the local path.

### Moving to shared team server

1. Deploy server with `RAG_API_KEY` set in env.
2. Developers run the ingest CLI once per project:
   `dotnet run --project tools/rag-dotnet/src/RagTools.Ingest -- --remote http://rag.internal:3001 --workspace .`
3. Update `.vscode/mcp.json` to use the remote SSE URL with `?project=<name>`.
4. Local Docker entries remain in mcp.json as fallback.

---

## Conformance checklist

### Ingest API
- [ ] `POST /ingest/{collection}` requires `X-Api-Key` header; returns 401 if missing or wrong
- [ ] `GET /ingest/{collection}/operations/{id}` requires `X-Api-Key` header
- [ ] `GET /ingest/{collection}/operations` (list) requires `X-Api-Key` header
- [ ] MCP tool endpoints (SSE / stdio) do NOT require `X-Api-Key`
- [ ] `GET /admin/stats` returns collection sizes and current `IDocumentStore` implementation name
- [ ] `RAG_API_KEY` unset at startup → 401 on all write endpoints + warning logged at startup
- [ ] Ingest worker uses a bounded `Channel<IngestJob>` — upload does not block on embedding
- [ ] Operation `expires_at` = `created_at + 5 days`; lazy sweep deletes expired points
- [ ] `config.yaml`, `multilingual-glossary.yaml`, `metadata-rules.yaml`, `queries.yaml` upserted to Qdrant on upload
- [ ] Ingest CLI reads `collection` from `config.yaml`; no separate `--collection` flag needed for standard use
- [ ] CLI supports `ops status <id>` and `ops list [--collection ...] [--status ...]`

### Storage and indexing
- [ ] All Qdrant search queries exclude `doc_kind` values prefixed with `__` and `"full_content"`
- [ ] Content points use a zero/dummy vector, not a real embedding
- [ ] Chunk point IDs are deterministic: `UUID(MD5("{collection}:{relPath}:{chunk_index}"))`
- [ ] Content point IDs are deterministic: `UUID(MD5("{collection}:{relPath}"))`
- [ ] Every chunk point payload contains `content_id` matching its content point's ID
- [ ] `chunk_index` is stored in every chunk point payload (required for neighbor computation)
- [ ] Config, glossary, rules, queries stored as structured JSON (not raw YAML text)
- [ ] `ContentDocument` has fields: `relPath`, `doc_kind`, `bc`, `title`, `content`, `ingested_at`, `metadata`
- [ ] `IDocumentStore` is the only path to storage — tools and worker do not call Qdrant directly

### Caching
- [ ] Config cache: no time expiry; invalidated on ingest for that collection; safety-net TTL via `RAG_CONFIG_CACHE_TTL_SECONDS`
- [ ] Glossary cache: same as config; `RAG_GLOSSARY_CACHE_TTL_SECONDS`
- [ ] Document content cache: 2 min default; `RAG_CONTENT_CACHE_TTL_SECONDS`; invalidated on ingest
- [ ] ADR list cache: 1 hour default; `RAG_ADR_LIST_CACHE_TTL_SECONDS`; invalidated on ingest
- [ ] Query embedding cache: 10 min default; `RAG_EMBED_CACHE_TTL_SECONDS`; key = `SHA256(expanded_text)`
- [ ] Chunk context cache: 2 min default; reuses `RAG_CONTENT_CACHE_TTL_SECONDS`
- [ ] Setting any `RAG_*_CACHE_TTL_SECONDS` to `0` disables that specific cache
- [ ] Search results are NOT cached

---

## References

- Related ADRs: ADR-0027 (RAG pipeline design)
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
