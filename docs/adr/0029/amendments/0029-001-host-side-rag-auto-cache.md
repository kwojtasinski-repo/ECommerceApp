# ADR-0029 Amendment 1: Host-Side RAG Auto-Cache Hook

> **Status**: Accepted (2026-05-28). Extends [ADR-0029](../0029-context-mode-mcp-sandbox.md) with an automated RAG → context-mode FTS5 cache pipeline that runs in the IDE host process, not inside the context-mode sandbox.

## Context

ADR-0029 introduced the context-mode MCP sandbox and its 5 lifecycle hooks (PreToolUse, PostToolUse, UserPromptSubmit, PreCompact, SessionStart). The L2 RAG fast path (`query_docs_cached`) packages a RAG result as a markdown payload plus a deterministic `source` label, but the agent still had to make a follow-up `ctx_index` call to persist it. That second call is:

- **Forgettable** — the agent skips it on roughly 1 in 4 turns under load.
- **Token-billed** — the agent has to emit the full markdown payload again as a tool argument.
- **Asymmetric across servers** — the .NET RAG and Python RAG return the same shape but the agent has to know which `mcp_<server>_<tool>` prefix to call.

Once the cache hit rate drops, the RAG-with-memory pattern degrades to plain RAG and the user re-pays the original embedding + ranking cost on every recall.

## Decision

Add a **host-side PostToolUse extension** that runs in the VS Code Copilot Chat hook chain (not inside the context-mode container) and:

1. **Detects** any tool call whose bare name belongs to a RAG MCP server (`ecommerceapp-rag-*` — stdio or HTTP variant).
2. **Formats** the response into markdown the FTS5 chunker can index. Formatter is **shape-driven** (looks for `files[].chunks[].text`, `hits[]`, `results[]` regardless of which RAG tool produced the output) rather than tool-name-dispatched.
3. **Persists** into context-mode's FTS5 store via a one-shot MCP stdio `ctx_index` call against `ecommerceapp-context-mode`.

The agent now makes **one** RAG call instead of two; the second leg (`ctx_index`) is automatic.

Pipeline shape:

```
Copilot tool call
      |
      v
PostToolUse hook envelope (stdin, JSON)
      |
      v
posttooluse-chain.ps1
      |---> docker exec ecommerceapp-context-mode (upstream context-mode hook)
      |---> node auto-cache.mjs
                    |
                    +---> detect RAG tool name -> format markdown
                    +---> docker exec ecommerceapp-context-mode mcp ctx_index
                                                |
                                                v
                                          FTS5 store
                                          (recall via ctx_search source="rag-auto-...")
```

## Architecture

### Files

| Path | Role |
|---|---|
| [`.github/hooks/context-mode.json`](../../../.github/hooks/context-mode.json) | Copilot Chat hook config — wires PostToolUse to `posttooluse-chain.ps1`. Unchanged behaviour for the other 4 hooks. |
| [`.github/hooks/posttooluse-chain.ps1`](../../../.github/hooks/posttooluse-chain.ps1) | Fan-out wrapper: pipes the hook envelope to both the upstream context-mode hook AND `auto-cache.mjs`. Best-effort: any failure is swallowed; `exit 0` keeps the chain alive. |
| [`.github/hooks/auto-cache.mjs`](../../../.github/hooks/auto-cache.mjs) | Node entry point. Detects RAG tool calls, formats markdown, calls `ctx_index`. |
| [`.github/hooks/.rag-tools-cache.json`](../../../.gitignore) | Runtime cache of the introspected RAG tool list (1h TTL). Gitignored. |
| [`.github/hooks/.rag-tools-cache.lock`](../../../.gitignore) | Coalescing lock for concurrent discovery kickoffs (60s TTL). Gitignored. |
| [`.github/hooks/auto-cache.log`](../../../.gitignore) | Diagnostic log. Gitignored. Enabled by default; opt-out with `AUTO_CACHE_DEBUG=0`. |

### Runtime tool discovery (L3)

The set of RAG tool names is **not** hardcoded. On every hook fire:

1. Read `.rag-tools-cache.json` — if fresh (<1h), use it.
2. If stale or missing, **return a hardcoded fallback set for THIS call** AND kick off a detached background introspection.
3. Background introspection parses `.vscode/mcp.json` (JSONC, string-aware comment stripper), filters for `type==="stdio"` and `name.startsWith("ecommerceapp-rag")`, then issues `tools/list` against each matched server over MCP stdio. Results are merged into a single set and written to `.rag-tools-cache.json` with `source: "introspected"`.
4. Concurrent hook fires race: the **lock file** ensures only one background introspection runs per 60-second window. The other kickoffs see the fresh lock and skip.

This means: a new RAG tool added to either RAG server upstream is auto-detected within one hook fire (the next call uses fallback; subsequent calls within 1h use the introspected set including the new tool).

**HTTP-only setups**: if `mcp.json` contains no stdio RAG entries, introspection logs a WARNING and falls back to the hardcoded list. Stdio and HTTP variants of the same RAG server expose identical tool names, so a single stdio entry in `mcp.json` is sufficient to cover both transports.

### Source labels

Auto-cached entries use the `rag-auto-` prefix (distinct from the agent-driven `rag-cache-` prefix used by manual `ctx_index` after `query_docs_cached`). Both prefixes are searchable via `ctx_search(source="rag-")`. Schema:

| Detected pattern | Source label |
|---|---|
| L2 wrapper response (has `source` field already) | Passthrough — uses the wrapper's own `rag-cache-...` label |
| `get_history` response (has `id`) | `rag-auto-adr<NNNN>` |
| Question contains ADR id | `rag-auto-adr<NNNN>-<hash8>` |
| `bc` filter present | `rag-auto-<slug(bc)>-<hash8>` |
| `query_docs` (orientation) | `rag-auto-orient-<hash8>` |
| Default | `rag-auto-q-<hash8>` |

`<hash8>` = first 8 hex chars of `sha256(question.lower().strip())`. Same question → same label → idempotent overwrite.

## Consequences

### Positive

- **One-call RAG-with-memory**: the agent no longer needs to remember the `ctx_index` follow-up.
- **Transport-agnostic**: works for stdio + HTTP variants of either RAG server — the formatter is shape-driven and the tool list is auto-discovered.
- **Zero blocking I/O**: discovery is detached + lock-coalesced; the hook chain stays under 300ms per fire.
- **Graceful degradation**: a broken RAG server, parser failure, or missing `mcp.json` all fall back to the hardcoded tool list; the hook never breaks the chain.
- **Observable**: `AUTO_CACHE_DEBUG=1` (default) writes a structured log of every fire, decision, and `ctx_index` outcome.

### Negative

- **Per-fire latency overhead**: ~290–310ms on the hot path (PS spawn + Node spawn + `docker exec` to context-mode). Acceptable because hooks run after the tool result is already delivered to the agent.
- **Cache-recency window for new RAG tools**: a brand-new RAG tool added upstream may be missed on its first invocation if the 1h cache is fresh. Mitigation: delete `.rag-tools-cache.json` to force a refresh.
- **Two source-label namespaces** (`rag-cache-` from manual, `rag-auto-` from hook). Both prefixed `rag-` so `ctx_search(source="rag-")` recalls everything.

### Risks & mitigations

- **Risk**: PowerShell exec policy blocks the wrapper. **Mitigation**: hook config passes `-ExecutionPolicy Bypass`.
- **Risk**: `auto-cache.mjs` throws. **Mitigation**: wrapper swallows non-zero exit and continues; `exit 0` always.
- **Risk**: `ctx_index` hangs. **Mitigation**: 5-second timeout on the `docker exec` for the index call.
- **Risk**: JSONC parser breaks on edge syntax in `mcp.json`. **Mitigation**: string-aware state machine (handles `//` inside `"http://..."`); failure logs and falls through to fallback list.

## Conformance checklist

- [x] Hook does not throw — `exit 0` on every code path.
- [x] Per-fire latency under 500ms p95 on the host path (measured: 290–310ms).
- [x] Discovery never blocks the hook chain (detached + unref'd).
- [x] Lock file expires after 60s so a crashed introspection does not poison subsequent runs.
- [x] Logs gated on `AUTO_CACHE_DEBUG` (default ON, opt-out with `=0`).
- [x] Cache + lock files gitignored.

## References

- Parent ADR: [ADR-0029 — context-mode MCP sandbox](../0029-context-mode-mcp-sandbox.md)
- L2 path: [ADR-0028 Amendment 3 — Transport-aware tools](../../0028/amendments/0028-003-transport-aware-tools.md) (`query_docs_cached`)
- Guide: [docs/rag/auto-cache-hook.md](../../../rag/auto-cache-hook.md)
- Routing rules: [.github/instructions/mcp-routing.instructions.md](../../../.github/instructions/mcp-routing.instructions.md)
