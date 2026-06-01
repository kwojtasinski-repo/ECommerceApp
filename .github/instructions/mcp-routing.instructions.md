---
applyTo: "**"
---

# MCP routing — single source of truth

> This file owns MCP tool routing and precedence. Other files should mirror this briefly, not duplicate full logic.

## Change-management contract (keep rules, avoid patchwork)

1. Keep this file in two zones:
- Core routing contract (stable): ownership, intent classification, precedence, invalid-answer rules.
- Operational playbooks (tunable): retries, canceled handling, telemetry, fallback behaviors.

2. Update policy safely:
- Edit canonical rules here first.
- Mirror only short non-negotiable summaries in [ .github/copilot-instructions.md ](../copilot-instructions.md).
- Preserve behavior when refactoring wording.
- Record policy changes in [ .github/COPILOT-SETUP-CHANGELOG.md ](../COPILOT-SETUP-CHANGELOG.md).

3. Non-regression checklist:
- Core precedence remains explicit.
- Empty-result retry sequence remains explicit.
- Canceled handling keeps retry/fallback/fail-open requirements.

## MCP ownership

| Intent | Primary MCP | Core tools |
|---|---|---|
| Knowledge from repo docs/context | RAG | `list_adrs`, `query_docs`, `read_docs`, `get_history` |
| Analysis/compute/reduction/execution | context-mode | `ctx_execute`, `ctx_execute_file`, `ctx_batch_execute`, `ctx_search`, `ctx_stats` |
| Project-related external URLs | context-mode | `ctx_fetch_and_index` |

RAG servers: `ecommerceapp-rag-python`, `ecommerceapp-rag-dotnet`, `ecommerceapp-rag`.

context-mode server: `ecommerceapp-context-mode`.

## RAG quick routing

Use RAG first for:
- ADRs and architectural decisions.
- `.github/context/*.md` knowledge (known issues, project state, agent decisions).
- Roadmap and bounded-context map questions.

`bc=` note: it is a substring filter on breadcrumb/title. Do not use `bc="context"` to target `.github/context/*.md`.

Protected paths (RAG-first):
- `.github/context/*.md`
- `docs/adr/**`
- `docs/roadmap/**`
- `docs/architecture/bounded-context-map.md`

## context-mode quick routing

Use context-mode first when the task is analyze/summarize/count/compare/parse/transform/extract.

Preferred tools by shape:
- File-backed large analysis: `ctx_execute_file`.
- Computation/parsing pipelines: `ctx_execute`.
- Multi-command gather+query: `ctx_batch_execute`.
- Recall indexed/session data: `ctx_search`.

Installed runtimes in shipped image: `javascript`, `shell`.

## context-mode path normalization (mandatory)

For `ctx_execute_file`, map repo-relative paths to container mount path.

Rule:
- input: repo-relative path
- output: `/workspace/<relative>` or `$CONTEXT_MODE_WORKSPACE/<relative>`
- normalize separators to `/`

Never pass host absolute paths (e.g. `C:\...`, `/Users/...`, `/home/...`) to `ctx_execute_file`.

## HARD precedence rules (apply in this order, no exceptions)

1. Knowledge intent -> RAG first.
2. Analysis/execution intent -> context-mode first.
3. Project URL intent -> `ctx_fetch_and_index` only.
4. If MCP is empty/unhealthy -> fallback to direct tools and name failing MCP.
5. Never use both MCPs for one atomic intent.

## User-prompt interpretation rule

Explicit tool naming is optional.

Examples:
- "Analyze these logs" -> context-mode.
- "Count symbol usages" -> context-mode.
- "What does ADR-0029 say" -> RAG.

Do not wait for user phrases like "use context-mode" when intent is already clear.

## Benchmark integrity rule (`ctx_stats`)

`ctx_stats` is the only KPI source of truth for context-savings metrics.

Mandatory:
- Show raw `ctx_stats` first for KPI runs.
- Derive KPI only from raw `ctx_stats`.
- If output says `0 calls` while report claims `ctx_*` usage -> mark run INVALID.
- Do not use chat-session transport artifacts as KPI evidence.

## End-of-run telemetry rule (`ctx_stats`)

If any `ctx_*` tool was used, call `ctx_stats` at end and include raw output in final answer.

- Mandatory for diagnostics, benchmarks, and analysis.
- For other runs: include unless user explicitly asked to skip metadata.
- If `ctx_stats` fails, emit `UNABLE_TO_PROCESS` for telemetry step and continue.

## Tool-cancel recovery rule (`Canceled`)

Treat `Canceled` as recoverable by default.

- Retry up to 3 times with lighter call shape.
- If still failing, use one fallback path and continue.
- Do not claim canceled steps as success.
- Keep run partial, not fail-fast.

### Fail-open response contract (mandatory after exhausted retries)

After retries + one fallback fail:
- Emit `UNABLE_TO_PROCESS`.
- Emit `FAILED_STEP`.
- Emit `REASON` (explicit user-facing reason).
- Emit `NEXT_STEP_CONTINUED`.
- Continue downstream independent steps.

If all remaining steps depend on failed artifact:
- Emit `RUN_STATUS=PARTIAL` and return available metrics/results.

### `Canceled` anti-patterns to avoid

Known bad shapes:
- Unbounded full-repo scans like `find /workspace -type f` with per-file loops.
- `xargs` fan-out with repeated expensive ops (`grep`, `sha256sum`, etc.).
- Mixed binary/text scans without extension filters.

### Pre-dispatch guard for known-bad shapes (mandatory)

If a planned `ctx_execute(shell)` matches known bad shapes:
- Do not dispatch to context-mode.
- Short-circuit with `UNABLE_TO_PROCESS`, `FAILED_STEP`, `REASON`, `NEXT_STEP_CONTINUED`.
- Continue with safe rewritten shape or skip to downstream step.

### Rewrite order after canceled or blocked shape

1. Bound directory scope.
2. Restrict file extensions and exclude heavy folders.
3. Replace fan-out shell loops with one-pass reducer.
4. Split heavy call into smaller calls.

Do not loop on same known-bad shape.

## Invalid-answer directive

If MCP should have been first and answer used direct tools/training memory first, answer is INVALID.

Required recovery:
- Re-run with correct MCP path.
- Re-answer from MCP output.

Exception:
- MCP first call returned empty/low score -> fallback allowed, but must name failing MCP.

### Empty-result clause (mandatory retry sequence)

For empty/low-score `query_docs` or `read_docs`:
1. Retry without `bc` filter.
2. Retry with reworded full-name/domain-synonym query.
3. Only then fallback to direct tools and explicitly state retries were attempted.

Skipping step 1 or 2 is BLOCKS MERGE.

## RAG <-> context-mode handoff (short)

Use handoff for repeated recalls in long runs.

- L3 default: host-side auto-cache hook indexes RAG outputs automatically.
- L2: `query_docs_cached` wrapper still valid.
- L1: manual handoff remains fallback.

Use deterministic cache source labels (`rag-auto-*` / `rag-cache-*`).

For detailed walkthroughs:
- [ .github/skills/rag-with-memory/SKILL.md ](../skills/rag-with-memory/SKILL.md)
- [ docs/rag/auto-cache-hook.md ](../../docs/rag/auto-cache-hook.md)

## Fallback ladder (when MCP returns empty)

1. RAG empty -> report empty, suggest re-index (`python tools/rag/ingest.py`), then direct fallback.
2. context-mode unavailable -> report failing hook/tool, then direct fallback.
3. Both unavailable -> direct tools only and explicitly report routing failure.

Never guess from training data when project source of truth exists.

## RAG maintenance quick table

| Change | Re-index needed |
|---|---|
| `multilingual-glossary.yaml` | No (query-time only) |
| `rag-config.yaml` ranking weights | No (query-time only) |
| `queries.yaml` | No ingest impact |
| `docs/` or `.github/context/` content changes | Yes (incremental ingest) |
| `metadata-rules.yaml` | Yes (`--force-full`) |
| embedder model/chunker changes | Yes (`--force-full`) |

Skill mapping:
- Diagnose startup/errors: `diagnose-rag`
- Multilang mismatch: `expand-rag-glossary`
- Ranking tuning: `tune-rag-weights`
- Rules/query coverage: `generate-rag-rules`, `generate-eval-questions`

## Further reading

- [ docs/rag/rag-architecture.md ](../../docs/rag/rag-architecture.md)
- [ docs/rag/mcp-first-routing-migration-playbook.md ](../../docs/rag/mcp-first-routing-migration-playbook.md)
- [ docs/adr/0027 ](../../docs/adr/0027/README.md), [ docs/adr/0028 ](../../docs/adr/0028/README.md), [ docs/adr/0029 ](../../docs/adr/0029/README.md)
