# RAG Ingest Pipeline — Decisions & Roadmap

> **Context**: This file records architectural decisions made during the RAG stabilisation sprint (May 2026) and the planned next steps. It is written for human readers, not for automated tooling. Operational agent-level corrections live in `.github/context/agent-decisions.md`.

---

## 1. Current state (HEAD = `828daaf6`, branch `RAG_Improvement`)

The RAG pipeline has two independent server implementations that share `tools/rag/rag-config.yaml`, `tools/rag/metadata-rules.yaml`, and `tools/rag/queries.yaml`:

| Server | Language | Port (HTTP) | Docker image | Collection |
|--------|----------|-----------|--------------|------------|
| Python | Starlette + sentence-transformers | 3002 | `rag-tools` | `ecommerceapp_docs` |
| .NET   | ASP.NET Core + ONNX | 3001 | `rag-dotnet` | `ecommerceapp_docs_dotnet` |

Both servers expose identical HTTP endpoints and MCP tool surface:

```
POST  /ingest/{collection}/batch                    → 202 | 400 | 503
GET   /ingest/{collection}/operations/{opId}        → 200 | 404
GET   /ingest/{collection}/operations               → 200
GET   /admin/stats                                  → 200
```

MCP tools: `query_docs`, `read_docs`, `get_history`, `list_adrs`.

---

## 2. Decisions made this sprint

### D-1 — ZIP validation required on every upload

**Decision**: Every `POST /ingest/{collection}/batch` must contain `metadata-rules.yaml` AND `queries.yaml` inside the uploaded ZIP.

**Validation rules (in order)**:
1. Both files present → 400 if missing
2. `metadata-rules.yaml` has at least one `doc_kind_rules` entry → 400 if empty
3. `queries.yaml` has at least one `named_queries` entry → 400 if empty
4. Every `doc_kind` referenced in `queries.yaml` has a matching `kind` in `metadata-rules.yaml` → 400 if unknown
5. After filtering out config files, at least one document remains → 400 if ZIP has no docs

**Why**: Without config files the server falls back to built-in defaults that don't match the repo's ADR folder structure, causing `adr_id` and `doc_kind` to be misdetected. Failing fast at upload time gives callers a clear error instead of silently misindexing.

**Implemented in**: `tools/rag/ingest_routes.py`, `tools/rag-dotnet/src/RagTools.Mcp/Controllers/IngestController.cs`

---

### D-2 — Two-level manifest design

**Decision**: The manifest is surfaced at two levels:

**Level 1 — Per-operation** (existing endpoint, already implemented):  
`GET /ingest/{collection}/operations/{opId}` returns `manifest:{indexedChunks, docKind}` when Completed. Absent for non-Completed statuses.

**Level 2 — Batch** (new endpoint, planned — not yet implemented):  
`GET /ingest/{collection}/batches/{batchId}` aggregates all per-file operations for a batch:

```json
// While processing:
{ "batchId": "...", "collection": "ecommerceapp_docs", "status": "Processing" }

// When all complete:
{
  "batchId":    "batch:ecommerceapp_docs:...",
  "collection": "ecommerceapp_docs",
  "status":     "Completed",
  "manifest": {
    "totalFiles":  3,
    "totalChunks": 26,
    "files": [
      { "relPath": "docs/adr/0001.md",     "chunks": 5,  "docKind": "adr_main" },
      { "relPath": "docs/concepts/ddd.md", "chunks": 12, "docKind": "concept"  },
      { "relPath": "docs/patterns/saga.md","chunks": 9,  "docKind": "pattern"  }
    ]
  },
  "indexStats": { "vectorCount": 1050 }
}
```

`batchId` is already returned in the 202 response from POST. The batch endpoint is not yet implemented — it requires `batchId` to be stored per-operation so all operations for a batch can be looked up.

**Why**: Per-operation is enough for polling individual files. Batch level gives callers one call to know "did my whole upload succeed and how many total chunks did I get". `indexStats.vectorCount` is a live Qdrant stat (cheap `count_points` call).

**Full design specification**: [ADR-0028 Amendment 002](../adr/0028/amendments/0028-002-batch-manifest-pipeline.md)

**Implemented**: Level 1 (per-operation) in commit `828daaf6`. Level 2 (batch endpoint) is planned (see F-3 in roadmap section).

---

### D-3 — `get_history` must bypass score threshold

**Decision**: `QueryEngine.search()` must NOT apply `score_threshold` when `field_filter` is set.

**Why**: `get_history(id="0006")` calls `search("history 0006", field_filter=("adr_id", "0006"))`. The query string `"history 0006"` has low cosine similarity to the TypedId ADR content (score ~0.24, below the `score_threshold=0.30`). The chunks are present in the index and correctly tagged with `adr_id="0006"`, but the threshold was silently filtering them out. The score is used for ranking only — when exact metadata filtering is active, all matching chunks should be returned regardless of vector score.

**Implemented in**: `tools/rag/query.py` — condition `if not field_filter:` wraps the threshold check.

---

### D-4 — `VECTOR_MODE` must not be baked into Docker image

**Decision**: The `tools/rag/Dockerfile` must not contain `ENV VECTOR_MODE=...`. The mode is set at runtime via `docker-compose.yaml` env sections or `--env` flag.

**Why**: `ENV VECTOR_MODE=local` baked into the image caused the container to use local (disk) Qdrant even when launched with `-e VECTOR_MODE=docker`. The env var silently overrode the intended runtime config.

---

## 3. Pipeline test status (2026-05-23)

**Result: 3 / 56 checks failing** — all pre-existing, not caused by this sprint.

Failing checks:
- `Python STDIO: get_history ADR-0006 returns chunks` — chunk_count=0
- `Python STDIO: get_history ADR-0006 has 'TypedId' in chunks` — chunk_count=0
- `Flow queries: TypedId pattern (ADR-0006)` — missing `['TypedId', 'abstract record']`

**Root cause analysis**: The score-threshold fix (`D-3`) is applied to the local Python source but the Phase 3 STDIO ingest uses the Qdrant Docker container and a locally-launched MCP process. If the Docker image hasn't been rebuilt after `D-3`, the fix doesn't apply to the STDIO path. **Rebuilding `rag-tools` image should resolve these 3 failures.**

---

## 4. Future plans

> Items marked ✅ were completed during the May 2026 stabilisation sprint.

### F-1 — Rebuild Docker images and re-run pipeline test ✅

Completed in commit `fb3a0636`. Both `rag-tools` and `rag-dotnet` images rebuilt. Full pipeline test: **56/56 PASSED**.

---

### F-2 — Persistent operation store (ADR-0028 Step 6)

**Current**: Operations are in-memory (`ConcurrentDictionary`), retained for 1 hour, lost on server restart.

**Planned**: Store operations as Qdrant points in a dedicated `__operations__` collection (or a separate collection per project). Survives restarts. Enables `GET /ingest/{collection}/operations` to return history beyond 1 hour.

**ADR-0028 context**: Alternative F (separate DB for ops) was explicitly recorded in ADR-0028 as a future option. This is the path when the 1-hour TTL is insufficient.

**Required work**:
1. Add `UpsertOperationAsync`, `FetchOperationAsync`, `ListOperationsAsync`, `DeleteExpiredOperationsAsync` to `IDocumentStore`
2. Implement in `QdrantDocumentStore` (Qdrant payload points, `doc_kind = "__op__"`)
3. Replace `ConcurrentDictionary` in `OperationStore` with Qdrant-backed storage
4. Update Python `operation_store.py` identically
5. Periodic sweep service (or lazy eviction on read) for `expires_at` enforcement
6. Add integration tests: restart server, confirm operations survive

**Complexity**: Medium-high (~3 days each server). No API contract changes.

---

### F-3 — Batch-level status endpoint (planned)

**Design**: `GET /ingest/{collection}/batches/{batchId}` — see D-2 above and [ADR-0028 Amendment 002](../adr/0028/amendments/0028-002-batch-manifest-pipeline.md) for the full contract.

**Required work**:
1. Store `batchId` on each `IngestOperation` at enqueue time (Python: add `batch_id` field to `IngestOperation` dataclass; .NET: add `BatchId` property to `IngestJob` + `IngestOperationResult`)
2. Implement `OperationStore.GetByBatch(batchId)` that returns all ops for a batchId (Python + .NET)
3. Add new route: `GET /ingest/{col}/batches/{batchId}` → controller action (Python: Starlette route; .NET: `IngestController` action)
4. Implement `indexStats.vectorCount` via Qdrant `count_points` at response time (not cached)
5. Implement status aggregation logic: any Queued/Processing → `"Processing"`; all Completed → `"Completed"`; mix → `"PartiallyFailed"`; all Failed → `"Failed"`
6. Add tests:
   - Unit: status aggregation (all permutations)
   - E2E: upload 3-file ZIP, poll batch endpoint → Processing, wait → Completed, assert `manifest.files` length = 3
   - E2E: 404 for unknown batchId

**Complexity**: Medium (~1 day each server). Pre-requisite: F-2 if batch must survive restarts (otherwise in-memory is fine to start with).

---

### F-4 — Python STDIO `get_history` ADR-0006 confirmation ✅

Resolved. After Docker rebuild (F-1), pipeline Phase 3 shows `chunk_count=18` for `get_history('0006')`. Score-threshold fix (D-3) confirmed working in container.

---

### F-5 — HTTP auth integration tests

**Current**: `X-Api-Key` middleware is implemented in both servers. Unit tests cover key validation. No integration test covers key rotation or invalid-key rejection end-to-end.

**Planned tests**:
1. `POST /ingest/...` without key when `RAG_API_KEY` is set → assert 401
2. `POST /ingest/...` with wrong key → assert 401
3. `POST /ingest/...` with correct key → assert 202
4. MCP tool endpoints (`/`, `/sse`) are not guarded → assert 200 without key

**Complexity**: Low (~0.5 days each server).

---

### F-6 — Transport migration (STDIO → HTTP Streamable as default dev workflow)

**Current**: VS Code MCP panel supports STDIO and HTTP Streamable. STDIO uses the Docker container per-invocation (slower startup). HTTP Streamable servers (ports 3001/3002) stay running and share Qdrant state. The Python server also still exposes a legacy SSE transport for back-compat.

**Planned**:
- Default dev workflow: HTTP-only (start `docker compose up -d rag-dotnet-http rag-python-http`)
- STDIO kept for CI / pipeline tests only
- Update `SETUP-GUIDE.md` to document HTTP-first setup
- Remove STDIO entries from `.github/mcp.json` default config (or make HTTP Streamable the first choice)

**Complexity**: Low — mostly documentation. No server code changes.

---

### F-7 — Named query management API

**Current**: `queries.yaml` is uploaded as part of the ZIP. There is no way to update named queries without re-uploading the entire collection.

**Planned**: Add `POST /ingest/{collection}/config/queries` accepting raw YAML (or JSON). Server validates, replaces the stored `__queries__` point in Qdrant. No re-chunking required.

**Why**: Named queries evolve faster than the document corpus. Allowing targeted updates avoids full re-ingestion just to add a query.

**Complexity**: Low (~0.5 days each server). Validation reuses existing `validate_queries_yaml` logic.

---

### F-8 — Chunker strategy selection at upload time

**Current**: Chunker settings (`max_tokens`, `overlap`) come from `rag-config.yaml` and are fixed for the collection lifetime.

**Planned**: Allow per-upload override in `metadata-rules.yaml` `chunker` block. Useful when uploading large architecture docs that need bigger chunks.

**Complexity**: Low (Python config already parsed from ZIP; just need to respect per-upload override). .NET side similar.

---

### F-9 — `list_documents(doc_kind)` — metadata-driven listing, no hardcoding

**Direction (not yet fully decided — to be refined when implementing)**:

The design principle: **no hardcoding of document taxonomy in the tool itself**. The available kinds to list come from what is ingested, which is driven by `doc_kind_rules` in `metadata-rules.yaml`.

**Analogy with `get_history`**:
- `get_history(id)` works because `adr_id` is stored in every Qdrant chunk payload (populated from `adr_id_patterns` in `metadata-rules.yaml`). The tool filters Qdrant by `adr_id = "0006"` → returns all matching chunks.
- `list(doc_kind)` would work the same way: `doc_kind` is stored in every Qdrant `full_content` point payload (populated from `doc_kind_rules`). The tool filters Qdrant by `doc_kind = "adr_main"` → returns all documents of that kind.

**What the caller sees**: only the `doc_kind` values that were actually ingested (i.e., exist in Qdrant). No list of kinds is hardcoded in the server. The metadata defines the taxonomy; the listing reflects exactly that taxonomy.

**Key constraint**: Keep it simple. The caller passes a `doc_kind` string. The server does one Qdrant filter query on `full_content` points. No complex schema, no config API surface for listing kinds.

**Current `list_adrs`**: is a specialised version of this — hardcoded to `doc_kind = "adr_main"` + filesystem read. The direction is to make it a Qdrant query instead.

**Deferred decision**: exact API shape (`list_documents(doc_kind)` vs `list(kind)` vs extended `list_adrs` with `kind` param). Decide when implementing.

**Why this is the right direction**: repo-agnostic, collection-aware, no server changes needed when a new `doc_kind` is added to `metadata-rules.yaml`.

---

### F-10 — Cache invalidation on re-ingest ⚠️ HIGH PRIORITY

**Current**: `CachedDocumentStore` (.NET) caches search results with a TTL. After re-ingesting a document (e.g., you updated ADR-0016 with new coupon limits), old cached results are served until the TTL expires. You re-ingest but Copilot still answers from stale chunks.

**Why this is the highest priority**: It silently serves outdated answers. The user thinks they updated the RAG index but the assistant keeps answering with the old content. There is no visible error — the bug is invisible.

**Fix (.NET)**: In `IngestWorker`, after `DeleteChunksAsync(collection, relPath, ct)` completes successfully, call `cache.Invalidate(relPath)` (or `cache.Clear()` for safety). The `IDocumentStore` interface (or `CachedDocumentStore` directly) needs an invalidation method.

**Fix (Python)**: Python server has no in-memory query cache currently — N/A until a cache is added. If a cache is added later, invalidation must be built in from the start.

**Required work (.NET)**:
1. Add `InvalidateAsync(string relPath)` to `IDocumentStore` (or expose on `CachedDocumentStore` directly)
2. Implement in `QueryCache`: remove all cache entries whose key contains `relPath`
3. Call from `IngestWorker` immediately after `DeleteChunksAsync` returns
4. Add unit test: ingest file, cache a query, re-ingest, assert cache miss on next query
5. Add integration test (optional): verify via HTTP that re-ingest returns fresh chunks

**Complexity**: Low (~0.5 days .NET). No API contract change.

---

## 5. Non-goals (deliberately excluded)

- **Multi-collection access control**: The current design has no per-collection auth. Any authenticated client can read/write any collection. No plans to change this.
- **Streaming ingest results**: The 202 + polling model is intentional. Server-sent events for ingest progress are not planned.
- **Chunking strategy configurability**: The chunker is config-driven via `rag-config.yaml[chunker]`. No UI or API surface for chunker config at upload time.
