# RAG Ingest Pipeline — Decisions & Roadmap

> **Context**: This file records architectural decisions made during the RAG stabilisation sprint (May 2026) and the planned next steps. It is written for human readers, not for automated tooling. Operational agent-level corrections live in `.github/context/agent-decisions.md`.

---

## 1. Current state (HEAD = `828daaf6`, branch `RAG_Improvement`)

The RAG pipeline has two independent server implementations that share `tools/rag/config.yaml`, `tools/rag/metadata-rules.yaml`, and `tools/rag/queries.yaml`:

| Server | Language | Port (SSE) | Docker image | Collection |
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

### D-2 — Manifest embedded in operation status response (no new endpoint)

**Decision**: When `GET /ingest/{collection}/operations/{opId}` returns a Completed operation, it includes a `manifest` block:

```json
{
  "operationId": "...",
  "status": "Completed",
  "collection": "ecommerceapp_docs",
  "relPath": "docs/adr/0001/0001-some-title.md",
  "enqueuedAt": "...",
  "startedAt": "...",
  "completedAt": "...",
  "manifest": {
    "indexedChunks": 12,
    "docKind": "adr_main"
  }
}
```

For non-Completed statuses the `manifest` key is absent. There is no new batch-level manifest endpoint.

**Why**: User explicitly requested using the existing endpoint rather than creating a new one. Per-operation granularity is sufficient — callers already poll individual operations from the 202 response's `operations` array.

**Implemented in**: `tools/rag/operation_store.py` + `ingest_worker.py`, `tools/rag-dotnet/src/RagTools.Core/IngestJob.cs` + `OperationStore.cs` + `IngestWorker.cs`

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

### F-1 — Rebuild Docker images and re-run pipeline test

**Why now**: The `rag-tools` image needs to be rebuilt to include the `query.py` score-threshold fix and the manifest changes. After rebuild, run `python test_full_pipeline.py --skip-build` to confirm 0 / 56 failures.

**Command**: `docker compose build rag-tools rag-dotnet` from repo root.

---

### F-2 — Persistent operation store (ADR-0028 Step 6)

**Current**: Operations are in-memory, retained for 1 hour, lost on server restart.

**Planned**: Store operations as Qdrant points in a dedicated `__operations__` collection (or a separate collection per project). Survives restarts. Enables `GET /ingest/{collection}/operations` to return history beyond 1 hour.

**Blocker**: None — can be done independently of other work. Medium complexity (~2 days).

---

### F-3 — Batch-level status endpoint

**Current**: `POST /ingest/.../batch` returns individual `operationId`s; callers must poll each.

**Planned**: When all operations in a batch are Completed (or any Failed), a single `GET /ingest/{collection}/batches/{batchId}` could return aggregate status. This was **deliberately deferred** — the per-operation endpoint is sufficient for now.

**Blocker**: Requires `batchId` to be stored alongside each operation (the `batchId` UUID is generated on upload but not persisted per-operation). Low complexity to add, but deprioritised.

---

### F-4 — Python STDIO `get_history` ADR-0006 confirmation

**Current**: 3 pipeline test failures suspect that the `rag-tools` Docker image needs rebuild. Confirm after F-1 is done.

**If failures persist after rebuild**: Investigate Qdrant payload for ADR-0006 chunks directly (check `adr_id` field value) to confirm the fix reaches the running container.

---

### F-5 — SSE auth for Python server

**Current**: Python SSE server supports `X-Api-Key` auth via `ApiKeyMiddleware`. The .NET server has the same. Both are optional (no-key configured = open).

**Planned**: Integration test covering key rotation and invalid-key rejection in both servers. Currently only the Python unit tests cover this path.

---

### F-6 — Transport migration (STDIO → SSE for primary dev workflow)

**Current**: VS Code MCP panel supports both STDIO and SSE. STDIO requires Docker run of the full image for each session. SSE servers (ports 3001/3002) stay running and share Qdrant state.

**Planned**: Default dev workflow should be SSE-only. STDIO kept for CI/pipeline tests only. Update `SETUP-GUIDE.md` to reflect this.

---

## 5. Non-goals (deliberately excluded)

- **Multi-collection access control**: The current design has no per-collection auth. Any authenticated client can read/write any collection. No plans to change this.
- **Streaming ingest results**: The 202 + polling model is intentional. Server-sent events for ingest progress are not planned.
- **Chunking strategy configurability**: The chunker is config-driven via `config.yaml[chunker]`. No UI or API surface for chunker config at upload time.
