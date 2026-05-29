# Bare-server integration probe — Addendum (F4 + F5 fixes)

**Date:** 2026-05-29 (follow-up)
**Branch:** `feat/rag-phase3-per-collection-config`
**Related:** [rag-bare-server-integration-2026-05-29.md](rag-bare-server-integration-2026-05-29.md), ADR-0028 Phase 3.

## Why an addendum

The original 11-step probe surfaced two real defects that the headline "11/11 PASS"
masked because they were observed in the Python server but worked-around manually:

- **F4** — `tools/rag/common.py` raised `KeyError` when a CLI workspace config omitted
  the `storage:` or `embedder:` sections. In `--remote` mode neither section is needed
  (the manifest path can default; the embedder lives on the server) but the loader
  insisted on both.
- **F5** — Python `IngestController.upload_batch` ingested ZIP-supplied data
  successfully but never invoked `DocumentStore.store_config(...)`, so per-collection
  config payloads were silently dropped on the floor. `DocumentStore.store_config`
  itself was unit-tested but unreachable from any production code path. The .NET
  server already wired this in commit 19f955e7 (P3-1/P3-5 ordering fix); Python did
  not.

This addendum documents the fixes and the re-run probe results.

## Code changes

| File | Change |
| --- | --- |
| `tools/rag/common.py` | `embedder_model` / `embedder_device` return `""` when section missing; `manifest_path` defaults to `.rag/manifest.json`. |
| `tools/rag/storage/document_store.py` | Added `ensure_collection(collection)` — creates the collection with the configured `VectorParams` if absent. |
| `tools/rag/ingest_routes.py` | `_parse_zip_batch` now builds a `RagConfigPayload` from the ZIP-supplied `rag-config.yaml` (chunker + ranking weights). `IngestController` accepts an optional `DocumentStore` and, on accepted batches, calls `ensure_collection(...)` then `store_config(...)` before enqueueing data points. `build_ingest_routes(...)` forwards the optional dep. |
| `tools/rag/mcp_server.py` | `_make_ingest_components(...)` constructs a `DocumentStore` from `state.ENGINE._client` + `state.ENGINE.embedder.dimensions` (with a defensive fallback that logs a WARN and disables persistence). Both `_run_sse` and `_run_http` pass the store through to `build_ingest_routes`. |
| `tools/rag/Dockerfile` | Added `COPY config/ ./config/` and `COPY storage/ ./storage/`. Previous `COPY *.py .` only baked flat files — the new sub-packages would have caused `ModuleNotFoundError: No module named 'config'` at server boot. |

## Re-run probe results

Same setup as the original probe (bare overlay `docker-compose.bare.yaml`, fresh
`tmp/bare-probe.zip`, Qdrant collections wiped between runs).

### F5 — Python now persists `__config__`

After the CLI re-uploaded `bare_probe_python` (data + ZIP-supplied `rag-config.yaml`):

```
GET /collections/bare_probe_python/points/count
  → { "count": 3 }    (was 2 before the fix — data only)

GET /collections/bare_probe_python/points  (id=0)
  → doc_kind: "__config__"
    schema_version: 2
    payload_json: { max_tokens: 800, overlap_tokens: 80, weights: [], ... }
```

The `__config__` sentinel matches the schema the .NET server has been writing all
along; the two implementations are now symmetric.

### F4 — CLI config without `storage:` / `embedder:`

Stripped both sections from `tmp/bare-cli-ws/tools/rag/rag-config-python.yaml`,
deleted the collection, re-ran the CLI:

```
[ingest] found 2 markdown files under ['docs']
[ingest] full rebuild — indexing all 2 files
[ingest] remote mode → http://rag-python-http:3002/ingest/bare_probe_python (2 file(s))
[ingest] batch ZIP ready (1614 bytes), POST → /ingest/bare_probe_python/batch
[ingest] batch accepted — 2 operation(s), polling …
[ingest] OK  docs/adr/0001/0001-typed-ids.md → ? chunk(s)
[ingest] OK  docs/architecture/overview.md → ? chunk(s)
[ingest] remote push complete: 2 ok, 0 failed, 0 skipped (2 total)
```

Previously this aborted with `KeyError: 'storage'` (or `'embedder'`) during config
load.

### .NET regression check

`bare_probe_dotnet` re-ingested via the same bare CLI workspace against
`http://rag-dotnet-http:3001` — 2 docs, 0 failures, 0 skipped. No regression from the
Python-side changes.

### Unit tests

```
tools/rag (.venv) — pytest test_ingest_api test_document_store test_config_payload test_config_sources
109 passed in 1.16s
```

## Outstanding follow-ups

- P3-7c..g still pending: read-path wiring (query-time application of the persisted
  `__config__` for chunker + ranking overrides).
- Bare overlay (`docker-compose.bare.yaml`, `tmp/bare-*`) intentionally left in place
  for further probing; tear down when Phase 3 closes.
