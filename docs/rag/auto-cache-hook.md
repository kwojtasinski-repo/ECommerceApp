# RAG Auto-Cache Hook — Operator Guide

> **Scope**: how the host-side PostToolUse hook auto-indexes RAG results into context-mode's FTS5 store, how to enable/disable it, how to debug it, and how to extend it.
>
> **Decision record**: [ADR-0029 Amendment 1](../adr/0029/amendments/0029-001-host-side-rag-auto-cache.md).
> **Routing impact**: [.github/instructions/mcp-routing.instructions.md](../../.github/instructions/mcp-routing.instructions.md) §L2 fast path.

## What it does

Every time Copilot Chat calls a RAG tool (`query_docs`, `read_docs`, `get_history`, `query_docs_cached`, `list_adrs`, …), the hook chain:

1. Receives the PostToolUse envelope from the Copilot host.
2. Pipes it to both the upstream context-mode SessionDB hook **and** our local `auto-cache.mjs`.
3. `auto-cache.mjs` detects the RAG tool name, formats the response as FTS5-friendly markdown, and issues a one-shot `ctx_index` call against the `ecommerceapp-context-mode` container.
4. The agent recalls the cached content later via `ctx_search(queries=[...], source="rag-auto-...")` — no RAG re-bill.

End result: **one RAG tool call ≈ one cache entry**, no agent-side follow-up needed.

## Pipeline at a glance

```
Copilot tool call result
        |
        v
  PostToolUse envelope (JSON on stdin)
        |
        v
  posttooluse-chain.ps1
        |---> docker exec ecommerceapp-context-mode  (upstream session capture)
        |---> node auto-cache.mjs
                    |
                    +-- detect RAG tool name (runtime-discovered list)
                    +-- shape-driven markdown formatter
                    +-- docker exec ecommerceapp-context-mode -> mcp/stdio ctx_index
                                                                       |
                                                                       v
                                                                 FTS5 store
                                                            (source="rag-auto-...")
```

## File layout

| Path | Role |
|---|---|
| `.github/hooks/context-mode.json` | Hook config consumed by Copilot Chat. Wires PostToolUse to the PS wrapper. |
| `.github/hooks/posttooluse-chain.ps1` | Fan-out wrapper. Best-effort. Always `exit 0`. |
| `.github/hooks/auto-cache.mjs` | Node hook entry. Two modes: `mainHook` (per-fire) and `runIntrospection` (background discovery). |
| `.github/hooks/.rag-tools-cache.json` | Discovered tool list. 1h TTL. Gitignored. |
| `.github/hooks/.rag-tools-cache.lock` | Coalescing lock for concurrent discoveries. 60s TTL. Gitignored. |
| `.github/hooks/auto-cache.log` | Diagnostic log. Gitignored. |

## Tool discovery (L3 — runtime introspection)

The list of RAG tool names is NOT hardcoded. The hook:

1. Checks `.rag-tools-cache.json` — if fresh (<1h), uses it.
2. Otherwise uses a hardcoded **fallback list** for the current fire AND spawns a detached background process that:
   - Parses `.vscode/mcp.json` (JSONC, string-aware comment stripper that respects `"http://..."`).
   - Filters for `type==="stdio"` AND `name.startsWith("ecommerceapp-rag")`.
   - Issues `tools/list` over MCP stdio against each match.
   - Merges results into a single set, writes `.rag-tools-cache.json` with `source: "introspected"`.
3. A **lock file** ensures only one background introspection runs per 60s window.

### Adding a new RAG tool upstream

No code change is needed in the hook. The next time the cache expires (or you delete `.rag-tools-cache.json`), the new tool will be auto-discovered.

### HTTP-only setups

If `.vscode/mcp.json` has no stdio RAG entries, introspection logs a `WARNING` and the hook falls back to the hardcoded list. Stdio and HTTP variants of the same RAG server expose identical tool names, so one stdio entry covers both transports.

## Source labels

| Detected pattern | Label format | Example |
|---|---|---|
| L2 wrapper response (`query_docs_cached`) | Passthrough — uses the wrapper's `source` field | `rag-cache-adr0029-3f8a1c20` |
| `get_history` (has `id` field) | `rag-auto-adr<NNNN>` | `rag-auto-adr0029` |
| Question mentions an ADR id | `rag-auto-adr<NNNN>-<hash8>` | `rag-auto-adr0028-9b4f2e10` |
| `bc=` filter present | `rag-auto-<slug(bc)>-<hash8>` | `rag-auto-presale-checkout-7e1d2a14` |
| `query_docs` (orientation) | `rag-auto-orient-<hash8>` | `rag-auto-orient-0b0e144e` |
| Default | `rag-auto-q-<hash8>` | `rag-auto-q-b369a526` |

All auto-cached entries are recallable via `ctx_search(queries=[...], source="rag-auto-")` (partial match). Manual L2 cached entries use the `rag-cache-` prefix instead; `ctx_search(source="rag-")` recalls both namespaces.

## Debug switch

| Env var | Effect |
|---|---|
| `AUTO_CACHE_DEBUG=1` (default) | Verbose log to `.github/hooks/auto-cache.log` from both the PS wrapper and Node. |
| `AUTO_CACHE_DEBUG=0` | Logging silenced in both tiers. The hook still runs. |

Set process-wide via PowerShell:

```powershell
$env:AUTO_CACHE_DEBUG = '0'   # for current session only
# or OS-level:
[System.Environment]::SetEnvironmentVariable('AUTO_CACHE_DEBUG', '0', 'User')
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `ctx_search(source="rag-auto-")` returns nothing | Hook never fired — Copilot didn't pick up the config | Reload the VS Code window once. The hook config hot-reloads on subsequent edits. |
| `auto-cache.log` shows `tools-cache hit (... source=fallback)` permanently | Stdio introspection is failing or no stdio RAG entries in `mcp.json` | Inspect log for `WARNING` / introspection errors. Verify `.vscode/mcp.json` parses (RAG servers should start manually from the MCP panel). |
| `auto-cache.log` shows `readJsonc(...) failed` | JSON syntax error in `.vscode/mcp.json` | Open in VS Code — the JSONC linter will pinpoint the column. |
| `ctx_index call returned` is missing for a fire that should have been cached | Tool name not in the active tool list | `Remove-Item .github/hooks/.rag-tools-cache.json` to force refresh; check next fire. |
| Multiple `background discovery kicked off` in rapid succession | Lock file is missing | Verify `.rag-tools-cache.lock` is writeable; check disk free space. |
| Hook fires but log lines are interleaved poorly | Concurrent tool calls — normal | The lock guarantees only one discovery runs, but per-fire log entries from parallel calls will interleave by timestamp. |

### Force a clean state

```powershell
Remove-Item .github/hooks/.rag-tools-cache.json,
            .github/hooks/.rag-tools-cache.lock,
            .github/hooks/auto-cache.log -Force -ErrorAction SilentlyContinue
```

Next RAG call will trigger fallback + background discovery; the call after that will use the introspected list.

## Limitations

- **Cache-recency window**: a brand-new RAG tool added upstream may be missed on its first invocation if the 1h cache is fresh. Delete `.rag-tools-cache.json` to force refresh.
- **HTTP introspection is a TODO**: only stdio servers are introspected. Pure-HTTP setups get the WARNING + fallback list.
- **Best-effort persistence**: `ctx_index` is not retried on failure. The diagnostic log records the failure; the user can re-issue the RAG call manually.
- **Windows-only PS wrapper**: the chain script is PowerShell. On non-Windows hosts, port `posttooluse-chain.ps1` to bash with the same `tee` semantics.

## Performance notes

| Stage | Median latency (ms) |
|---|---|
| PS wrapper spawn + read stdin | 200–250 |
| Upstream context-mode hook (`docker exec`) | 200–280 |
| Node hook (`docker exec` for `ctx_index`) | 30–80 |
| **Total added by hook chain** | **~290–310 ms** |

This runs after the tool result is already delivered to the agent; it does not appear in the agent's wall-clock turn time. The lock + background-discovery design ensures the per-fire path never blocks on the 30–40s introspection sweep.
