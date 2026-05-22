# ADR-0028 Amendment 1: Implementation Deviations from Original Design

## Date
2026-05-21

## Author
GitHub Copilot (implementation session)

## Summary

Phase 1 (.NET) has been fully implemented. This amendment records deviations from the
original ADR-0028 design that were made during implementation.

---

## Deviation 1: OperationStore — In-Memory, Not Qdrant-Backed

**Original design (Step 6):** `OperationStore` wraps a separate Qdrant `__ops__` collection
with structured points, `expires_at` field, and a periodic sweep service.

**Implemented:** `OperationStore` uses an in-memory `ConcurrentDictionary<string, IngestOperationResult>`
with 1-hour TTL retention and access-time eviction (stale entries removed on `Get()` access).

**Rationale:**
- Simpler implementation, zero Qdrant overhead
- Eliminates dependency on a second collection (reduces risk of collection management errors)
- The HTTP polling use case does not require persistence across server restarts
- Clients that poll and receive no response simply re-ingest (idempotent by design)
- Can be upgraded to Qdrant-backed storage in a future ADR without API changes

**Impact on API contract:** none — the HTTP response shape is unchanged.

---

## Deviation 2: IngestJob — Single File Per Job

**Original design:** `IngestJob` contains `IReadOnlyList<(string RelPath, string Text)> Files`,
allowing multiple files to be batched into a single job.

**Implemented:** One `IngestJob` per file. Each POST to `/ingest/{collection}` enqueues
exactly one document, returning one `operationId`. Clients that need to upload multiple files
POST them sequentially (or in parallel with separate requests).

**Rationale:**
- Per-file operation tracking is more granular (users can see which specific file failed)
- Channel capacity (1000) and worker throughput are sufficient for sequential uploads
- Simpler retry logic: re-POST a single failed file vs re-POSTing a batch

**Impact on API contract:** The CLI remote mode (`--remote`) uploads files one-by-one, which
matches this design. The server API is compatible with future batching (a batch endpoint could
create multiple single-file jobs internally).

---

## Deviation 3: RagSession — Scoped in SSE, Singleton in Stdio

**Original design:** `RagSession` was described as a single scoped service.

**Implemented:**
- **SSE mode**: `RagSession` is registered as `Scoped` — each HTTP request/session gets its own
  instance, resolved from `?project=` query param by `RagSessionMiddleware`.
- **Stdio mode**: `RagSession` is registered as `Singleton` — no HTTP context, uses `cfg.Collection`
  as the fixed collection for the process lifetime.

This means stdio mode is single-collection (unchanged from ADR-0027 behaviour).
Multi-collection support requires SSE mode with `?project=` query param.

---

## New Components (not in original ADR)

| Component | Location | Purpose |
|---|---|---|
| `RagSession` | `RagTools.Core/RagSession.cs` | Scoped session-level collection binding |
| `RagSessionMiddleware` | `RagTools.Mcp/Middleware/RagSessionMiddleware.cs` | Resolves `?project=` into `RagSession` |
| `ApiKeyMiddleware` | `RagTools.Mcp/Middleware/ApiKeyMiddleware.cs` | Guards `/ingest/*` and `/admin/*` |
| `IngestController` | `RagTools.Mcp/Controllers/IngestController.cs` | HTTP ingest endpoints |
| `IngestChannel` | `RagTools.Core/IngestChannel.cs` | Bounded `Channel<IngestJob>` (cap: 1000) |
| `IngestJob` | `RagTools.Core/IngestJob.cs` | Single-file job descriptor |
| `IngestWorker` | `RagTools.Core/IngestWorker.cs` | `BackgroundService` processing the channel |
| `OperationStore` | `RagTools.Core/OperationStore.cs` | In-memory job status tracking |
| `QueryCache` | `RagTools.Core/QueryCache.cs` | Bounded LRU cache (512 entries), per-key TTL |
| `CachedDocumentStore` | `RagTools.Core/CachedDocumentStore.cs` | `IDocumentStore` caching decorator |
| `RagConfigPayload` | `RagTools.Core/RagConfigPayload.cs` | Serializable config snapshot for Qdrant |
| `DeterministicId` | `RagTools.Core/DeterministicId.cs` | UUID v3 (MD5) for stable point IDs |

---

## Test Coverage

New unit tests added in `RagTools.Tests`:
- `QueryCacheTests` — TTL expiry, cache hit/miss, prefix invalidation, clear
- `OperationStoreTests` — state transitions, unknown op no-op, collection filtering
- `IngestChannelTests` — TryWrite, WriteAsync/ReadAsync round-trip, payload preservation
- `ApiKeyMiddlewareTests` — non-protected path bypass, dev mode (no RAG_API_KEY), auth rejection
