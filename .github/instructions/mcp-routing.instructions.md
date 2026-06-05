---
applyTo: "**"
---

# MCP routing — single source of truth

> Compact canonical policy. Keep this file short and stable.
> If behavior changes, update this file first, then mirror brief summaries elsewhere.

## MCP ownership

| Intent | Primary MCP | Core tools |
|---|---|---|
| Knowledge from repo docs/context | RAG | `list_adrs`, `query_docs`, `read_docs`, `get_history` |
| Analysis/compute/reduction/execution | context-mode | `ctx_execute`, `ctx_execute_file`, `ctx_batch_execute`, `ctx_search`, `ctx_stats` |
| Project-related external URLs | context-mode | `ctx_fetch_and_index` |

RAG servers: `ecommerceapp-rag-python`, `ecommerceapp-rag-dotnet`, `ecommerceapp-rag`.

context-mode server: `ecommerceapp-context-mode`.

## Core precedence (mandatory)

1. Knowledge intent -> RAG first.
2. Analysis/execution intent -> context-mode first.
3. Project URL intent -> `ctx_fetch_and_index` only.
4. If MCP is empty/unhealthy -> fallback to direct tools and name failing MCP.
5. Never use both MCPs for one atomic intent.

## RAG quick rules

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

Empty/low-score `query_docs`/`read_docs` retry order:
1. Retry without `bc`.
2. Retry with reworded full-name/domain-synonym query.
3. Only then fallback to direct tools and state retries were attempted.

## context-mode quick rules

Use context-mode first when the task is analyze/summarize/count/compare/parse/transform/extract.

Preferred tools by shape:
- File-backed large analysis: `ctx_execute_file`.
- Computation/parsing pipelines: `ctx_execute`.
- Multi-command gather+query: `ctx_batch_execute`.
- Recall indexed/session data: `ctx_search`.

Installed runtimes in shipped image: `javascript`, `shell`.

### context-mode execution contract (always-on)

Apply this by default for every analysis run, even when the user does not ask explicitly:

1. Start bounded-first (small probe), then expand only if needed.
2. Never start with full-repo recursive scans.
3. Use `javascript` as the default runtime for file processing and parsing.
4. Use `shell` only for small, bounded command probes.
5. Any non-`javascript` runtime requires an explicit availability check first (`ctx_doctor` and, when needed, a one-line smoke probe).
6. If the requested runtime is unavailable, auto-fallback to `javascript` or bounded `shell` and continue.
7. Return synthesis + evidence, not raw dumps.

Bounded-first shape requirements:
- Scope: explicit folders only (no repo root wildcard).
- File types: explicit extensions only.
- Exclusions: always exclude `bin` and `obj` for code scans.
- Result caps: include match limits (`-m`) and output caps (`head`).

Escalation rule:
- Probe 1: smallest viable scope.
- Probe 2: broaden one axis only (scope OR pattern OR cap).
- Probe 3: broaden one more axis only if probe 2 is still insufficient.
- Never widen all axes at once.

## context-mode path normalization (mandatory)

For `ctx_execute_file`, map repo-relative paths to container mount path.

Rule:
- input: repo-relative path
- output: `/workspace/<relative>` or `$CONTEXT_MODE_WORKSPACE/<relative>`
- normalize separators to `/`

Never pass host absolute paths (e.g. `C:\...`, `/Users/...`, `/home/...`) to `ctx_execute_file`.

## User-prompt interpretation rule

Explicit tool naming is optional.

Examples:
- "Analyze these logs" -> context-mode.
- "Count symbol usages" -> context-mode.
- "What does ADR-0029 say" -> RAG.

Do not wait for user phrases like "use context-mode" when intent is already clear.

## End-of-run telemetry (`ctx_stats`)

If any `ctx_*` tool was used, call `ctx_stats` at end and include raw output in final answer.

- Mandatory for diagnostics, benchmarks, and analysis.
- For other runs: include unless user explicitly asked to skip metadata.
- If `ctx_stats` fails, emit `UNABLE_TO_PROCESS` for telemetry step and continue.

## MCP cancel recovery rule (`Canceled`) — all MCP servers

Treat `Canceled` as recoverable by default.

- Default long-wait threshold: 5 minutes for MCP operations that may run long.
- When an MCP tool supports a timeout parameter, set `timeout=300000` for potentially long-running calls by default.
- Retry up to 5 times with lighter call shape.
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

If a planned MCP call matches known bad shapes:
- Do not dispatch to context-mode.
- Short-circuit with `UNABLE_TO_PROCESS`, `FAILED_STEP`, `REASON`, `NEXT_STEP_CONTINUED`.
- Continue with safe rewritten shape or skip to downstream step.

### Rewrite order after canceled or blocked shape

1. Bound directory scope.
2. Restrict file extensions and exclude heavy folders.
3. Replace fan-out shell loops with one-pass reducer.
4. Split heavy call into smaller calls.

### Deterministic canceled retry sequence (mandatory)

When any MCP call returns `Canceled` or times out:

1. Retry once with narrower scope.
2. Retry once with narrower pattern.
3. Retry once with lower match/output caps.
4. Retry once by splitting work into smaller independent calls.
5. Retry once with reduced query set/labels.
6. If still failing, emit fail-open contract fields and continue independent steps.

Do not retry the same command shape verbatim.

Do not loop on same known-bad shape.

### User risk-acceptance fallback (after 5 failed retries)

When MCP remains unavailable after retry sequence:
- Ask user to accept a higher-token direct-tools fallback.
- If accepted, proceed with bounded direct file search/read and explicitly mark increased token risk.
- If not accepted, stop at partial status with clear next step options.

## Invalid-answer directive

If MCP should have been first and answer used direct tools/training memory first, answer is INVALID.

Required recovery:
- Re-run with correct MCP path.
- Re-answer from MCP output.

Exception:
- MCP first call returned empty/low score -> fallback allowed, but must name failing MCP.

## RAG <-> context-mode handoff (short)

Use handoff for repeated recalls in long runs.

- L3 default: host-side auto-cache hook indexes RAG outputs automatically.
- L2: `query_docs_cached` wrapper still valid.
- L1: manual handoff remains fallback.

Use deterministic cache source labels (`rag-auto-*` / `rag-cache-*`).

For detailed walkthroughs:
- [ .github/skills/rag-with-memory/SKILL.md ](../skills/rag-with-memory/SKILL.md)
- [ docs/rag/auto-cache-hook.md ](../../docs/rag/auto-cache-hook.md)

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
