# ADR-0028 Amendment 2: Batch Ingest API, Manifest Design, and Ingest Pipeline

## Date
2026-05-23

## Author
GitHub Copilot (stabilisation sprint, May 2026)

## Status
Accepted (implementation pending for batch endpoint; per-operation manifest already implemented)

## Summary

This amendment documents three additions to ADR-0028:

1. **ZIP upload with validation** — the batch upload endpoint now requires `metadata-rules.yaml`
   and `queries.yaml` inside every uploaded ZIP.
2. **Per-operation manifest** — the existing status endpoint returns chunk/kind info on completion.
3. **Batch status endpoint** — a planned new endpoint aggregates all per-operation results into
   a single batch-level response with files manifest and index stats.

It also documents the ingest processing pipeline in detail.

---

## Addition 1: ZIP Validation Requirements

Every `POST /ingest/{collection}/batch` must upload a ZIP that:

1. Contains `metadata-rules.yaml` — document classification and ADR-ID extraction rules.
2. Contains `queries.yaml` — named queries for the MCP `query_docs` tool.
3. Has at least one `doc_kind_rules` entry in `metadata-rules.yaml`.
4. Has at least one `named_queries` entry in `queries.yaml`.
5. Has no `doc_kind` in `queries.yaml` that is missing from `metadata-rules.yaml`.
6. Contains at least one non-config file after the config files are filtered out.

Validation failures return `400 Bad Request` with a JSON body describing the error.
The config files are NOT indexed — they are extracted for validation/configuration only.

**Rationale**: Without per-upload config, the server falls back to built-in defaults that
do not match this repo's ADR folder structure, leading to silent misclassification. Failing
fast at upload time is better than silently misindexing.

**Implemented in**: `tools/rag/ingest_routes.py`, `tools/rag-dotnet/src/RagTools.Mcp/Controllers/IngestController.cs`

---

## Addition 2: Per-Operation Manifest (Implemented)

`GET /ingest/{collection}/operations/{opId}` now returns a `manifest` block **only** when
`status == "Completed"`. Non-completed statuses omit the field entirely.

### While processing (Queued or Processing)

```json
{
  "operationId": "...",
  "collection":  "ecommerceapp_docs",
  "relPath":     "docs/adr/0001/0001-some-title.md",
  "status":      "Processing",
  "enqueuedAt":  "2026-05-23T10:00:00Z",
  "startedAt":   "2026-05-23T10:00:01Z"
}
```

### When completed

```json
{
  "operationId":  "...",
  "collection":   "ecommerceapp_docs",
  "relPath":      "docs/adr/0001/0001-some-title.md",
  "status":       "Completed",
  "enqueuedAt":   "2026-05-23T10:00:00Z",
  "startedAt":    "2026-05-23T10:00:01Z",
  "completedAt":  "2026-05-23T10:00:03Z",
  "manifest": {
    "indexedChunks": 12,
    "docKind":        "adr_main"
  }
}
```

**Implemented in**: `tools/rag/operation_store.py` + `ingest_worker.py`,
`tools/rag-dotnet/src/RagTools.Core/IngestJob.cs` + `OperationStore.cs` + `IngestWorker.cs`

---

## Addition 3: Batch Status Endpoint (Planned — Not Yet Implemented)

### Motivation

`POST /ingest/{collection}/batch` already accepts a ZIP with multiple files and returns a
`batchId` alongside the per-file `operations` list. Callers can poll each operation
individually, but there is no single endpoint that aggregates the entire batch result.

### Design

```
GET /ingest/{collection}/batches/{batchId}
```

#### While any operation is still in progress

```json
{
  "batchId":    "batch:ecommerceapp_docs:550e8400-e29b-41d4-a716-446655440000",
  "collection": "ecommerceapp_docs",
  "status":     "Processing"
}
```

#### When all operations have completed (or any failed)

```json
{
  "batchId":    "batch:ecommerceapp_docs:550e8400-e29b-41d4-a716-446655440000",
  "collection": "ecommerceapp_docs",
  "status":     "Completed",
  "manifest": {
    "totalFiles":  3,
    "totalChunks": 26,
    "files": [
      { "relPath": "docs/adr/0001.md",        "chunks": 5,  "docKind": "adr_main" },
      { "relPath": "docs/concepts/ddd.md",     "chunks": 12, "docKind": "concept"  },
      { "relPath": "docs/patterns/saga.md",    "chunks": 9,  "docKind": "pattern"  }
    ]
  },
  "indexStats": {
    "vectorCount": 1050
  }
}
```

`status` values:
- `"Processing"` — at least one operation is not yet Completed/Failed
- `"Completed"` — all operations completed successfully
- `"PartiallyFailed"` — some operations failed; `manifest.files` lists only the successful ones
- `"Failed"` — all operations failed; no `manifest`
- `"NotFound"` (404) — batchId unknown or expired (> 1 hour)

### Implementation notes (for when this is built)

1. `batchId` must be stored alongside each `IngestOperation` so the batch endpoint can
   look up all operations for a given batchId. Currently `batchId` is generated on upload
   but not persisted per-operation.
2. `indexStats.vectorCount` requires a Qdrant `count_points` call at response time (no cache;
   this is a live stat). The call is cheap but has a network round-trip.
3. Status is derived by scanning all operations for the batchId:
   - any Queued or Processing → `Processing`
   - all Completed → `Completed`
   - mix of Completed + Failed → `PartiallyFailed`
   - all Failed → `Failed`
4. Batch retention matches operation retention (1 hour from enqueue time).

---

## Ingest Processing Pipeline Detail

This section documents what happens inside the server when a document is ingested.

### Input

One `IngestJob`:
```
OperationId  — unique op ID (returned to caller)
Collection   — Qdrant collection / project
RelPath      — repo-relative path (e.g. "docs/adr/0006/0006-typedid.md")
Content      — full text of the markdown file (UTF-8)
DocKind      — optional override; if null, auto-detected from RelPath
EnqueuedAt   — timestamp when the job was queued
```

### Processing steps

#### Step 1 — Document classification

```python
doc_kind = detect_doc_kind(rel_path, cfg)
```

Applies `doc_kind_rules` from `metadata-rules.yaml` in order; first matching glob wins.
Example rules (simplified):
```yaml
doc_kind_rules:
  - { glob: "**/amendments/**",             kind: "adr_amendment" }
  - { glob: "**/example-implementation/**", kind: "adr_example"   }
  - { glob: "docs/adr/**",                  kind: "adr_main"      }
  - { glob: ".github/context/**",           kind: "context"       }
```
No match → `"other"`.

#### Step 2 — ADR ID extraction

```python
adr_id = detect_adr_id(rel_path, cfg)
```

Applies `adr_id_patterns` from `metadata-rules.yaml` in order; first match wins. The named
capture group `id` is extracted. Example:
- `docs/adr/0006/0006-typedid.md` → regex `adr/(?P<id>\d{4})/` → `adr_id = "0006"`
- `docs/concepts/ddd.md` → no match → `adr_id = None`

`adr_id` is stored in every chunk payload and is the field used by `get_history(id)` to
retrieve all chunks belonging to a specific ADR.

#### Step 3 — Chunking

```python
chunks = chunk_markdown(content, doc_title=..., chunker_cfg=cfg.chunker)
```

The chunker splits a markdown document into semantically coherent chunks:
- Splits on heading boundaries (`#`, `##`, `###`, etc.)
- Each chunk has a token budget: `max_tokens=512` (configurable)
- Overlap: `overlap=64` tokens between adjacent chunks (prevents context loss at boundaries)
- Each chunk carries: `breadcrumb` (heading path), `heading_path`, `start_line`, `end_line`,
  `token_count`, `text`

The breadcrumb is the concatenation of ancestor headings, e.g.:
`"ADR-0016: Multi-coupon pipeline > 3. Validation rules > 3.1 Max coupons"`

#### Step 4 — Embedding

```python
vectors = model.encode([chunk.text for chunk in chunks], normalize_embeddings=True)
```

- Model: `paraphrase-multilingual-MiniLM-L12-v2` (384-dim, multilingual)
- Normalised to unit length (cosine similarity = dot product after normalisation)
- Batched (default batch size 64) to control memory usage

In .NET: `OnnxEmbedder` runs the same ONNX-exported model. Texts are pre-processed as
`"{breadcrumb}\n\n{text}"` (breadcrumb prepended to give the embedding context from the
heading hierarchy).

#### Step 5 — Delete old chunks (idempotent re-ingest)

```python
client.delete(collection, filter={"rel_path": rel_path})
```

Removes all existing vector points for this `rel_path` before upserting new ones.
This makes re-uploading the same file fully safe — old chunks are replaced, not duplicated.

#### Step 6 — Upsert new chunks

Each chunk becomes a Qdrant `PointStruct`:
```json
{
  "id":     "<stable hash of rel_path + breadcrumb + start_line>",
  "vector": [0.123, -0.456, ...],
  "payload": {
    "rel_path":     "docs/adr/0006/0006-typedid.md",
    "doc_title":    "ADR-0006: TypedId and Value Objects",
    "doc_kind":     "adr_main",
    "adr_id":       "0006",
    "breadcrumb":   "ADR-0006 > 2. Decision",
    "heading_path": "ADR-0006 > 2. Decision",
    "start_line":   45,
    "end_line":     67,
    "token_count":  184,
    "weight":       1.2,
    "text":         "We adopt TypedId<T> as the standard..."
  }
}
```

The stable chunk ID is derived from `blake2b(rel_path + breadcrumb + start_line)` (Python)
or `UUID(MD5("{collection}:{relPath}:{chunkIndex}"))` (.NET). This means re-ingesting the
same file with the same structure produces the same point IDs (idempotent).

#### Step 7 — Store full-content point

A separate "full content" point is stored (not a vector search target) so `read_docs` can
return the entire raw document without a second semantic search.

Python: `client.upsert(id=0, ...)` with `doc_kind="__content__"`.
.NET: `StoreDocumentAsync(collection, contentDoc, ct)` via `IDocumentStore`.

#### Step 8 — Mark completed

```python
await store.mark_completed(op_id, chunk_count=len(points), doc_kind=doc_kind)
```

The operation transitions: `Queued → Processing → Completed`. The chunk count and doc_kind
are stored and surfaced via the `manifest` block in the operation status endpoint.

### Query pipeline

When `query_docs` is called:

1. Expand query (multilingual glossary — prepend English expansion ×3 for non-ASCII queries).
2. Encode query → 384-dim vector.
3. Qdrant vector search with optional payload filter (`doc_kind`, `adr_id`, or `bc_filter`).
4. Apply `score_threshold=0.30` (skip this step when using `field_filter` — exact metadata queries).
5. Apply weight multiplier (`payload.weight`) and re-sort.
6. Return top `top_k` hits.

`get_history(id)` skips the score threshold (see D-3 in `decisions-and-roadmap.md`).
