# RAG Pipeline Test Report

Generated: 2026-05-22 14:44 UTC  
Branch: `RAG_Improvement`

## Summary

| Phase | Status | Elapsed | Checks |
|---|---|---|---|
| Prerequisites | ✅ PASSED | 1.2s | 4/4 |
| Stop SSE containers | ✅ PASSED | 7.7s | 1/1 |
| Docker build --no-cache | ✅ PASSED | 0.0s | 1/1 |
| Python STDIO — ingest + query | ✅ PASSED | 45.6s | 5/5 |
| .NET STDIO — ingest + query | ✅ PASSED | 105.6s | 6/6 |
| SSE servers — start + HTTP ingest + query | ✅ PASSED | 20.5s | 9/9 |
| Flow queries via Docker STDIO | ✅ PASSED | 9.7s | 7/7 |
| Hosted ingest via HTTP API (no volume mounts) | ✅ PASSED | 5.1s | 8/8 |

**Total**: 41/41 checks passed ✅

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
- ✅ get_adr_history ADR-0006 has 'TypedId'
- ✅ read_docs returns files — `2 files`

### .NET STDIO — ingest + query

- ✅ .NET ingest --force-full
- ✅ MCP initialize handshake
- ✅ query_docs returns ADR-0006 content — `17292 chars`
- ✅ query_docs contains 'TypedId'
- ✅ get_adr_history ADR-0006 has content — `21968 chars`
- ✅ list_adrs returns known ADRs — `1382 chars`

### SSE servers — start + HTTP ingest + query

- ✅ docker compose up --force-recreate (both SSE)
- ✅ Python SSE port 3002 reachable
- ✅ .NET SSE port 3001 reachable
- ✅ Python SSE: query_docs → ADR-0016 (coupons) — `hits: ['docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md', 'docs/adr/0016/0016-sales-coupons-bc-design.md', 'docs/adr/0016/README.md']`
- ✅ Python SSE: get_adr_history ADR-0016 mentions 'coupon'
- ✅ .NET SSE: MCP initialize handshake — `session=1m9FeCT8…`
- ✅ .NET SSE: tools/list — `['get_adr_history', 'list_adrs', 'read_docs', 'query_docs']`
- ✅ .NET SSE: query_docs → ADR-0016 (coupons) — `12570 chars`
- ✅ .NET SSE: get_adr_history ADR-0016 mentions 'coupon' — `60902 chars`

### Flow queries via Docker STDIO

- ✅ MCP handshake for flow queries
- ✅ Coupon limit rule (ADR-0016)
- ✅ Order lifecycle (ADR-0014)
- ✅ Cross-BC event communication (ADR-0010)
- ✅ TypedId pattern (ADR-0006)
- ✅ Known .NET upgrade issues
- ✅ Saga / orchestration decision (ADR-0026)

### Hosted ingest via HTTP API (no volume mounts)

- ✅ Python SSE: POST /config — metadata rules uploaded — `status=200`
- ✅ Python SSE: POST /ingest → 202 Accepted — `status=202 opId=34aaa873-f8f4-452a-925a-f74925c13e43`
- ✅ Python SSE: ingest operation Completed — `status=Completed`
- ✅ Python SSE: uploaded doc queryable via MCP — `hits: ['docs/hosted-ingest-e2e-test.md', 'docs/adr/0028/amendments/0028-001-implementation-deviations.md', '.github/context/known-issues.md', 'docs/adr/0001/0001-project-overview-and-technology-stack.md', 'docs/adr/0010/amendments/a1-retry-observability-configuration.md']`
- ✅ .NET SSE: POST /config — metadata rules uploaded — `status=200`
- ✅ .NET SSE: POST /ingest → 202 Accepted — `status=202 opId=ecommerceapp_docs_dotnet:docs-hosted-ing`
- ✅ .NET SSE: ingest operation Completed — `status=Completed`
- ✅ .NET SSE: uploaded doc queryable via MCP — `14153 chars`

## Notes & Improvement Suggestions

- **Docker build time**: `.NET` image downloads ONNX model from HuggingFace (~100 MB)
  on every `--no-cache` build. Consider caching the model layer separately or
  using a private registry mirror for CI/CD.

- **Python SSE transport**: Uses legacy `SseServerTransport` (two-endpoint SSE + POST).
  The .NET server uses the newer MCP Streamable HTTP standard. Consider migrating
  the Python server to `streamablehttp` transport when mcp-python supports it.

- **API key enforcement**: The `.NET` SSE server enforces `X-Api-Key` via `ApiKeyMiddleware`.
  The Python SSE server has no auth guard. Add one for production use.

- **Collection separation**: Python uses `ecommerceapp_docs`, .NET uses
  `ecommerceapp_docs_dotnet`. Both are indexed independently (different embedders).
  Consider a single canonical collection if embedding parity is achieved.

- **STDIO cold start**: .NET STDIO requires 2–3s for ONNX model load.
  Python STDIO requires 3–5s for sentence-transformers model load.
  Both are acceptable for VS Code MCP spawn (one-time cost).
