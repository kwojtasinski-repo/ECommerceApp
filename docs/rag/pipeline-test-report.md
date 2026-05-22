# RAG Pipeline Test Report

Generated: 2026-05-22 13:41 UTC  
Branch: `RAG_Improvement`

## Summary

| Phase | Status | Elapsed | Checks |
|---|---|---|---|
| Prerequisites | ✅ PASSED | 0.9s | 4/4 |
| Stop SSE containers | ✅ PASSED | 2.7s | 1/1 |
| Docker build --no-cache | ✅ PASSED | 0.0s | 1/1 |
| Python STDIO — ingest + query | ✅ PASSED | 42.4s | 5/5 |
| .NET STDIO — ingest + query | ✅ PASSED | 102.9s | 6/6 |
| SSE servers — start + HTTP ingest + query | ✅ PASSED | 20.7s | 9/9 |
| Flow queries via Docker STDIO | ✅ PASSED | 10.3s | 7/7 |

**Total**: 33/33 checks passed ✅

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
- ✅ query_docs returns ADR-0006 content — `14653 chars`
- ✅ query_docs contains 'TypedId'
- ✅ get_adr_history ADR-0006 has content — `18357 chars`
- ✅ list_adrs returns known ADRs — `2152 chars`

### SSE servers — start + HTTP ingest + query

- ✅ docker compose up --force-recreate (both SSE)
- ✅ Python SSE port 3002 reachable
- ✅ .NET SSE port 3001 reachable
- ✅ Python SSE: query_docs → ADR-0016 (coupons) — `hits: ['docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md', 'docs/adr/0016/0016-sales-coupons-bc-design.md', 'docs/adr/0016/README.md']`
- ✅ Python SSE: get_adr_history ADR-0016 mentions 'coupon'
- ✅ .NET SSE: MCP initialize handshake — `session=nmvq533o…`
- ✅ .NET SSE: tools/list — `['get_adr_history', 'list_adrs', 'read_docs', 'query_docs']`
- ✅ .NET SSE: query_docs → ADR-0016 (coupons) — `10521 chars`
- ✅ .NET SSE: get_adr_history ADR-0016 mentions 'coupon' — `49218 chars`

### Flow queries via Docker STDIO

- ✅ MCP handshake for flow queries
- ✅ Coupon limit rule (ADR-0016)
- ✅ Order lifecycle (ADR-0014)
- ✅ Cross-BC event communication (ADR-0010)
- ✅ TypedId pattern (ADR-0006)
- ✅ Known .NET upgrade issues
- ✅ Saga / orchestration decision (ADR-0026)

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
