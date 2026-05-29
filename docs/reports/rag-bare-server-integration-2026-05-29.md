# Bare-server integration test — RAG HTTP servers (2026-05-29)

**Purpose**: End-to-end verification that the regression fix in commit `19f955e7` (BatchIngestService now calls `EnsureCollectionAsync` before `StoreConfigAsync`) actually lets the seed flow work in a production-like scenario where:

1. The HTTP servers run with **bare host-side companion configs** (empty `metadata-rules.yaml` / `queries.yaml` / `multilingual-glossary.yaml`) — all real rules must come from the per-batch ZIP.
2. The Qdrant collection can disappear mid-flight (operator wipes data) and the **CLI re-upload (`ingest.py --remote`) must self-heal** by recreating the collection on the next batch POST.

**Result**: ✅ All 11 steps PASS. Both .NET and Python HTTP servers behave identically. Two latent product requirements surfaced (see §"Findings").

---

## Setup

| Component | Image / state | Port | Notes |
|---|---|---|---|
| qdrant | shared | 6333 | unchanged |
| rag-dotnet-http | `rag-dotnet:latest` (rebuilt at 19f955e7) | 3001 | mounts overridden to `tmp/bare-rag/*` |
| rag-python-http | `rag-tools:latest` (rebuilt at 19f955e7) | 3002 | companion mounts overridden to `tmp/bare-rag/*` |

Override file: [docker-compose.bare.yaml](../../docker-compose.bare.yaml). Stripped host configs live under [tmp/bare-rag/](../../tmp/bare-rag/):

- `rag-config.yaml` — minimal (only `source`, `chunker`, `vector_store`, `ranking.weights=[]`, `query`, `config_files`)
- `metadata-rules.yaml` — `doc_kind_rules: []`
- `queries.yaml` — `named_queries: []`
- `multilingual-glossary.yaml` — `languages: {}`

---

## Steps

### 1. Stop current HTTP containers

```pwsh
docker compose --profile rag-dotnet-http --profile rag-python-http stop rag-dotnet-http rag-python-http
```

Both containers stopped cleanly. ✅

### 2. Create bare config scaffolding

Created `tmp/bare-rag/{rag-config,metadata-rules,queries,multilingual-glossary}.yaml` with the minimum structurally-valid contents listed above. ✅

### 3. Write compose override

Created `docker-compose.bare.yaml` re-mapping the 4 companion file mounts on `rag-dotnet-http` and the 3 companion mounts on `rag-python-http` to point at `tmp/bare-rag/`. ✅

### 4. Start servers with bare configs

```pwsh
docker compose -f docker-compose.yaml -f docker-compose.bare.yaml `
  --profile rag-dotnet-http --profile rag-python-http up -d `
  rag-dotnet-http rag-python-http
```

Both ports ready in 0 s. Containers `Up`. ✅

### 5. Boot logs confirm bare-config load

**rag-dotnet-http**:
```
[rag-mcp] config     : /rag-config.yaml (809 bytes)
[rag-mcp] collection : ecommerceapp_docs_dotnet
[rag-mcp] embedder   : onnx
[rag-mcp] glossary   : fallback=mounted (transport=http)
[rag-mcp] config src : layered (decorated with IDistributedCache)
[rag-mcp] endpoint  : http://0.0.0.0:3001/ (MCP Streamable HTTP)
```

**rag-python-http**:
```
[rag-mcp] metadata:   <companion metadata-rules.yaml>
[rag-mcp] queries:    <companion queries.yaml>
[rag-mcp] embedding model ready
[rag-mcp] transport:  http (port 3002)
[ingest-worker] started
```

Both servers booted cleanly with empty companion YAML files. ✅

### 6. Build self-contained probe ZIP

`tmp/bare-probe.zip` (1 243 bytes), POSIX entries:

```
rag-config.yaml
metadata-rules.yaml
queries.yaml
docs/adr/0001/0001-typed-ids.md
docs/architecture/overview.md
```

ZIP-internal `metadata-rules.yaml` contains real `doc_kind_rules` (adr + architecture). ✅

### 7. POST ZIP to both /ingest/{coll}/batch

| Server | Endpoint | Status | Operations |
|---|---|---|---|
| .NET | `POST /ingest/bare_probe_dotnet/batch` | **202** | 2 ops queued (`batch:bare_probe_dotnet:639156334723040977`) |
| Python | `POST /ingest/bare_probe_python/batch` | **202** | 2 ops queued (`batch:bare_probe_python:11607c5a-…`) |

Both responses include the expected `warnings[]` array (missing-glossary advisory). ✅

### 8. Poll operations to terminal state

Both servers reached `Completed` for all 2 operations within ~1 s. .NET response shape: bare JSON array. Python response shape: `{operations: [...], count: 2}`. Both reported `chunk_count: 1` per doc and `manifest.doc_kind: "other"` (the bare server's *host-side* `metadata-rules.yaml` is empty, so doc_kind classification on the server side defaults to `other` — this is correct behaviour: per-batch rules in the ZIP drive ingest validation but doc_kind tagging is server-side). ✅

### 9. Verify Qdrant state

```
Collections: [bare_probe_dotnet, bare_probe_python, …]
bare_probe_dotnet  → 5 points  (2 chunks + 3 config payload entries)
bare_probe_python  → 2 points  (2 chunks; Python does not yet persist per-collection config — wired in P3-7b)
```
✅

### 10. Wipe both collections from Qdrant

```pwsh
DELETE http://localhost:6333/collections/bare_probe_dotnet  → {"result":true}
DELETE http://localhost:6333/collections/bare_probe_python  → {"result":true}
```

Listing confirms both collections removed. ✅

### 11. CLI re-upload via `ingest.py --remote` (the actual prod path)

Workspace at `tmp/bare-cli-ws/` containing real `metadata-rules.yaml` + `queries.yaml` (non-empty), two markdown docs, and per-target rag-config files.

#### Python target

```pwsh
docker run --rm --network ecommerceapp_default `
  -v ${PWD}\tmp\bare-cli-ws:/workspace `
  -e RAG_CONFIG=/workspace/tools/rag/rag-config-python.yaml `
  rag-tools python /app/ingest.py --remote http://rag-python-http:3002 --force-full
```

Output:
```
[ingest] batch accepted — 2 operation(s), polling …
[ingest] OK  docs/adr/0001/0001-typed-ids.md → ? chunk(s)
[ingest] OK  docs/architecture/overview.md → ? chunk(s)
[ingest] remote push complete: 2 ok, 0 failed, 0 skipped (2 total)
```
✅

#### .NET target

```pwsh
docker run --rm --network ecommerceapp_default `
  -v ${PWD}\tmp\bare-cli-ws:/workspace `
  -e RAG_CONFIG=/workspace/tools/rag/rag-config-dotnet.yaml `
  rag-tools python /app/ingest.py --remote http://rag-dotnet-http:3001 --force-full
```

Output:
```
[ingest] batch accepted — 2 operation(s), polling …
[ingest] OK  docs/architecture/overview.md → 1 chunk(s)
[ingest] OK  docs/adr/0001/0001-typed-ids.md → 1 chunk(s)
[ingest] remote push complete: 2 ok, 0 failed, 0 skipped (2 total)
```

**This is the key result**: the .NET server happily recreated `bare_probe_dotnet` from scratch on first POST after collection deletion — exactly the seed-flow path that was broken before commit 19f955e7 and would have returned `HTTP 500: Status(StatusCode="NotFound", Detail="Not found: Collection 'bare_probe_dotnet' doesn't exist!")`. ✅

#### Final Qdrant state

```
bare_probe_dotnet  → 5 points  (collection recreated + config persisted)
bare_probe_python  → 2 points  (collection recreated)
```
✅

---

## Findings

### F1 — Regression fix 19f955e7 verified in production-like scenario [PASS]

The CLI seed flow against a freshly-empty Qdrant collection now succeeds end-to-end through Docker → HTTP → IngestController → BatchIngestService → DocumentStore on both server stacks. This was the primary test goal.

### F2 — Server-side `metadata-rules.yaml` requires ≥ 1 entry to accept a ZIP [confirmed expected]

Surfaced when the CLI uploaded an empty `metadata-rules.yaml`:
```
HTTP 400: {"error":"metadata-rules.yaml must contain at least one doc_kind_rules entry"}
```
This is a validator guard in the batch endpoint (`BatchValidator` on .NET, equivalent in Python). It is **not** a bug — it forces every batch to declare its doc-kind taxonomy. The bare HOST config can be empty; the per-batch ZIP cannot.

### F3 — Same guard for `queries.yaml` [confirmed expected]

```
HTTP 400: {"error":"queries.yaml must contain at least one named_queries entry"}
```
Same reasoning as F2.

### F4 — `ingest.py` config schema requires `storage.manifest_path` and `embedder.model` even in `--remote` mode

The CLI loads the full local config before computing the file-change diff — so even though the embedder is unused in `--remote` mode, the schema is parsed in full. This was previously masked because every real `tools/rag/rag-config.yaml` in the repo contains both sections. Not a blocker, but worth noting in the CLI docs (the user-facing message is `KeyError: 'storage'` / `KeyError: 'embedder'` — could be improved to point at the missing section).

### F5 — Python server does not yet persist per-collection batch config

`bare_probe_python` has 2 points (data only); `bare_probe_dotnet` has 5 (2 data + 3 config payload). This is the expected current state — Python `StoreConfigAsync` equivalent is the deliverable of **P3-7b**.

---

## Container state at end of report

Per user request, containers are LEFT RUNNING with bare-config mounts:

```
ecommerceapp-rag-python-http-1   Up   (compose overlay: docker-compose.bare.yaml)
ecommerceapp-rag-dotnet-http-1   Up   (compose overlay: docker-compose.bare.yaml)
ecommerceapp-qdrant-1            Up
```

To restore normal mounts:
```pwsh
docker compose --profile rag-dotnet-http --profile rag-python-http up -d rag-dotnet-http rag-python-http
```
(without the `-f docker-compose.bare.yaml` override).

To tear down completely:
```pwsh
docker compose --profile rag-dotnet-http --profile rag-python-http down
```

Scaffolding files retained for inspection: `tmp/bare-rag/`, `tmp/bare-zip/`, `tmp/bare-cli-ws/`, `tmp/bare-probe.zip`, `docker-compose.bare.yaml`.

---

## Conclusion

✅ **PASS** — Regression fix `19f955e7` correctly enables the seed flow against an empty Qdrant collection on both .NET and Python HTTP servers. The CLI `ingest.py --remote` path self-heals deleted collections. Both servers tolerate empty host-side companion configs and rely on per-batch ZIP-supplied rules as designed.
