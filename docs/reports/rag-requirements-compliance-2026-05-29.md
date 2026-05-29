# RAG requirements and decisions audit — 2026-05-29

## Scope

This report verifies:

- current RAG requirements status
- key decisions recorded in agent-decisions and ADR/roadmap docs
- implementation compliance in Python and .NET code
- remaining gaps

Audit sources:

- [docs/roadmap/rag-remote-multitenant.md](../roadmap/rag-remote-multitenant.md)
- [.github/context/agent-decisions.md](../../.github/context/agent-decisions.md)
- [tools/rag/common.py](../../tools/rag/common.py)
- [tools/rag/ingest_routes.py](../../tools/rag/ingest_routes.py)
- [tools/rag/rag-config.yaml](../../tools/rag/rag-config.yaml)
- [tools/rag-dotnet/rag-config.yaml](../../tools/rag-dotnet/rag-config.yaml)
- [tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs](../../tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs)
- [tools/rag-dotnet/src/RagTools.Core/BatchIngestService.cs](../../tools/rag-dotnet/src/RagTools.Core/BatchIngestService.cs)
- [tools/rag/test_http_servers.py](../../tools/rag/test_http_servers.py)

## Requirements status

### 1) User-facing config reduced to tunables only (embedder/storage removed)

Status: PASS

Evidence:

- Python config has source/chunker/vector_store/ranking/query/config_files sections and no embedder or storage section: [tools/rag/rag-config.yaml](../../tools/rag/rag-config.yaml)
- .NET config has source/chunker/vector_store/ranking/query/config_files sections and no embedder or storage section: [tools/rag-dotnet/rag-config.yaml](../../tools/rag-dotnet/rag-config.yaml)

### 2) Server-side embedder defaults and silent ignore of removed embedder keys

Status: PASS

Evidence:

- Server-owned default embedder model constant and explicit ignore note in code: [tools/rag/common.py](../../tools/rag/common.py#L25)
- embedder_model returns default constant, not YAML value: [tools/rag/common.py](../../tools/rag/common.py#L71)

### 3) Mode-aware vector store behavior (HTTP vs STDIO/local)

Status: PASS

Evidence:

- HTTP mode forces default collection and URL in Python config accessors: [tools/rag/common.py](../../tools/rag/common.py#L81), [tools/rag/common.py](../../tools/rag/common.py#L101)

### 4) rag-config.yaml mandatory in batch ZIP (both stacks)

Status: PASS

Evidence:

- Python validator requires rag-config.yaml, metadata-rules.yaml, queries.yaml in ZIP root: [tools/rag/ingest_routes.py](../../tools/rag/ingest_routes.py#L89)
- .NET validator requires rag-config.yaml and companion files from config mapping/fallback names: [tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs](../../tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs#L39)

### 5) get_history tool contract aligned and validated on both HTTP servers

Status: PASS

Evidence:

- Live smoke script expects query_docs/read_docs/get_history on both servers: [tools/rag/test_http_servers.py](../../tools/rag/test_http_servers.py#L112), [tools/rag/test_http_servers.py](../../tools/rag/test_http_servers.py#L177)
- Smoke verifies get_history payload has chunks list: [tools/rag/test_http_servers.py](../../tools/rag/test_http_servers.py#L143), [tools/rag/test_http_servers.py](../../tools/rag/test_http_servers.py#L207)

### 6) Per-collection config persistence path (Phase 3) in runtime code

Status: PASS for implemented sub-steps

Evidence:

- .NET stores config before enqueue and invalidates config cache: [tools/rag-dotnet/src/RagTools.Core/BatchIngestService.cs](../../tools/rag-dotnet/src/RagTools.Core/BatchIngestService.cs#L74)
- Python stores config before enqueue and invalidates config source: [tools/rag/ingest_routes.py](../../tools/rag/ingest_routes.py#L360)

### 7) Full completion of roadmap phase

Status: PARTIAL (one item still open)

Evidence:

- Remaining roadmap item explicitly listed as P3-8 pending: [docs/roadmap/rag-remote-multitenant.md](../roadmap/rag-remote-multitenant.md#L868), [docs/roadmap/rag-remote-multitenant.md](../roadmap/rag-remote-multitenant.md#L910)

## Decisions confirmed

### Key decisions from agent-decisions

- Batch ZIP must include metadata-rules and queries and fail fast on missing/invalid files: [.github/context/agent-decisions.md](../../.github/context/agent-decisions.md#L148)
- get_history threshold behavior corrected for field_filter mode: [.github/context/agent-decisions.md](../../.github/context/agent-decisions.md#L162)
- RAG auto-cache hook (L3) promoted and routed as default memory path: [.github/context/agent-decisions.md](../../.github/context/agent-decisions.md#L54)

### Current roadmap decision state

- Stabilization update states dual-stack hardening complete and keeps only P3-8 pending: [docs/roadmap/rag-remote-multitenant.md](../roadmap/rag-remote-multitenant.md#L932)

## Code scan findings

### Finding A — primary requirements are implemented

Severity: High confidence pass

Both stacks implement the scoped requirements from this sprint (config reduction, server defaults, mandatory ZIP config contract, get_history alignment, dual-stack validation path).

### Finding B — one remaining implementation gap is still open by design

Severity: Medium

P3-8 cross-collection integration tests are not done yet (documented as pending).

### Finding C — legacy helper scripts still reference removed get_adr_history naming

Severity: Low

Some non-runtime helper scripts still call get_adr_history instead of get_history.

Examples:

- [tools/rag/manual_query.py](../../tools/rag/manual_query.py#L166)
- [tools/rag/test_flows.py](../../tools/rag/test_flows.py#L240)

This does not block production MCP servers, but it is drift and can confuse manual validation workflows.

## Overall verdict

- Requirements implemented in runtime code: YES
- Decisions documented and reflected in roadmap: YES
- Remaining work to close this track fully: P3-8 tests + optional cleanup of legacy helper script naming

## Suggested next actions

1. Complete P3-8 integration tests in both stacks and mark roadmap item done.
2. Align legacy helper scripts from get_adr_history to get_history.
3. After 1 and 2, update roadmap status line from in progress to complete for this track.
