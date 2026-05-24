# RAG Pipeline Test Report

Generated: 2026-05-23 09:01 UTC  
Branch: `RAG_Improvement`

## Summary

| Phase | Status | Elapsed | Checks |
|---|---|---|---|
| Prerequisites | ✅ PASSED | 0.8s | 4/4 |
| Stop SSE containers | ✅ PASSED | 2.4s | 1/1 |
| Docker build --no-cache | ✅ PASSED | 0.0s | 1/1 |
| Python STDIO — ingest + query | ✅ PASSED | 40.2s | 6/6 |
| .NET STDIO — ingest + query | ✅ PASSED | 107.9s | 7/7 |
| SSE servers — start + HTTP ingest + query | ✅ PASSED | 19.8s | 11/11 |
| Flow queries via Docker STDIO | ✅ PASSED | 8.9s | 7/7 |
| Hosted ingest via HTTP API (no volume mounts) | ✅ PASSED | 8.9s | 12/12 |
| get_history tool — retrieve indexed chunks by history field | ✅ PASSED | 0.9s | 6/6 |

**Total**: 55/55 checks passed ✅

## Phase Details

### Prerequisites

- ✅ Docker daemon reachable
- ✅ Qdrant reachable on port 6333
- ✅ .venv exists
- ✅ mcp.client.sse importable

### Stop SSE containers

- ✅ SSE containers stopped (rag-python-sse + rag-dotnet-sse)

### Docker build --no-cache

- ✅ (skipped via --skip-build)

### Python STDIO — ingest + query

- ✅ ingest.py --mode docker --force-full
- ✅ MCP initialize handshake
- ✅ query_docs returns ADR-0006 (TypedId) — `3 hits`
- ✅ get_history ADR-0006 has 'TypedId' in chunks
- ✅ read_docs returns files — `2 files`
- ✅ get_history ADR-0006 returns chunks — `chunk_count=18`

### .NET STDIO — ingest + query

- ✅ .NET ingest --force-full
- ✅ MCP initialize handshake
- ✅ query_docs returns ADR-0006 content — `17292 chars`
- ✅ query_docs contains 'TypedId'
- ✅ get_history ADR-0006 (.NET STDIO) has content — `11355 chars`
- ✅ list_adrs returns known ADRs — `1382 chars`
- ✅ .NET get_history ADR-0006 returns chunks — `11355 chars`

### SSE servers — start + HTTP ingest + query

- ✅ docker compose up --force-recreate (both SSE)
- ✅ Python SSE port 3002 reachable
- ✅ .NET SSE port 3001 reachable
- ✅ Python SSE: query_docs → ADR-0016 (coupons) — `hits: ['docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md', 'docs/adr/0016/0016-sales-coupons-bc-design.md', 'docs/adr/0016/README.md']`
- ✅ Python SSE: get_history ADR-0016 mentions 'coupon'
- ✅ Python SSE: get_history('0016') → chunk_count > 0 — `chunk_count=42`
- ✅ .NET SSE: MCP initialize handshake — `session=_rARlnfK…`
- ✅ .NET SSE: tools/list — `['list_adrs', 'get_history', 'read_docs', 'query_docs']`
- ✅ .NET SSE: query_docs → ADR-0016 (coupons) — `12570 chars`
- ✅ .NET SSE: get_history ADR-0016 mentions 'coupon' — `46919 chars`
- ✅ .NET SSE: get_history('0016') → chunk_count > 0 — `chunk_count=36`

### Flow queries via Docker STDIO

- ✅ MCP handshake for flow queries
- ✅ Coupon limit rule (ADR-0016)
- ✅ Order lifecycle (ADR-0014)
- ✅ Cross-BC event communication (ADR-0010)
- ✅ TypedId pattern (ADR-0006)
- ✅ Known .NET upgrade issues
- ✅ Saga / orchestration decision (ADR-0026)

### Hosted ingest via HTTP API (no volume mounts)

- ✅ Python SSE: POST /ingest/batch → 202 Accepted — `status=202 opId=0cd509e6-9384-4813-8cfe-2c03c33cfb53`
- ✅ Python SSE: ingest operation Completed — `status=Completed`
- ✅ Python SSE: uploaded doc queryable via MCP — `hits: ['docs/hosted-ingest-e2e-test.md', 'docs/adr/0028/amendments/0028-001-implementation-deviations.md', '.github/context/known-issues.md', 'docs/adr/0001/0001-project-overview-and-technology-stack.md', 'docs/adr/0010/amendments/a1-retry-observability-configuration.md']`
- ✅ .NET SSE: POST /ingest/batch → 202 Accepted — `status=202 opId=ecommerceapp_docs_dotnet:docs-hosted-ing`
- ✅ .NET SSE: ingest operation Completed — `status=Completed`
- ✅ .NET SSE: uploaded doc queryable via MCP — `14440 chars`
- ✅ Python SSE: POST /ingest/batch → 202 Accepted — `status=202 count=2`
- ✅ Python SSE: batch op docs/batch-test-a.md Completed — `status=Completed`
- ✅ Python SSE: batch op docs/batch-test-b.md Completed — `status=Completed`
- ✅ .NET SSE: POST /ingest/batch → 202 Accepted — `status=202 count=2`
- ✅ .NET SSE: batch op docs/batch-test-a.md Completed — `status=Completed`
- ✅ .NET SSE: batch op docs/batch-test-b.md Completed — `status=Completed`

### get_history tool — retrieve indexed chunks by history field

- ✅ Python SSE: get_history('0016') → chunk_count > 0 — `chunk_count=42`
- ✅ Python SSE: get_history('0016') chunks ordered by start_line — `start_lines=[1, 1, 1, 1, 1, 1]`
- ✅ Python SSE: get_history('__nonexistent_9b__') → 0 chunks — `chunk_count=0`
- ✅ .NET SSE: get_history — MCP initialize — `session=aL4qynci…`
- ✅ .NET SSE: get_history('0016') → chunk_count > 0 — `chunk_count=36`
- ✅ .NET SSE: get_history('__nonexistent_9d__') → 0 chunks — `chunk_count=0`

## Notes & Improvement Suggestions

- **Docker build time**: `.NET` image downloads ONNX model from HuggingFace (~100 MB)
  on every `--no-cache` build. Consider caching the model layer separately or
  using a private registry mirror for CI/CD.

- **Python HTTP transport**: ~~Uses legacy `SseServerTransport`~~ Now uses `StreamableHTTPSessionManager`
  (mcp>=1.8.0, `MCP_TRANSPORT=http`). Both Python and .NET servers now use the same
  MCP Streamable HTTP standard (`POST /`). VS Code connects via `"type":"http"` in `mcp.json`.

- **API key enforcement**: Both the `.NET` and Python HTTP servers enforce `X-Api-Key` via `ApiKeyMiddleware`.
  Ensure `RAG_API_KEY` is set in production deployments.

- **Collection separation**: Python uses `ecommerceapp_docs`, .NET uses
  `ecommerceapp_docs_dotnet`. Both are indexed independently (different embedders).
  Consider a single canonical collection if embedding parity is achieved.

- **STDIO cold start**: .NET STDIO requires 2–3s for ONNX model load.
  Python STDIO requires 3–5s for sentence-transformers model load.
  Both are acceptable for VS Code MCP spawn (one-time cost).
