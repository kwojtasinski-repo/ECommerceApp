---
applyTo: "**"
---

# MCP routing — single source of truth

> Compact canonical policy. Keep this file short and stable.
> Start with [agent-workflow.instructions.md](agent-workflow.instructions.md) for the short front door.
> If behavior changes, update this file first, then mirror brief summaries elsewhere.

## Top Rules

- RAG = use the repo-doc MCP servers: `ecommerceapp-rag-python`, `ecommerceapp-rag-dotnet`, or `ecommerceapp-rag`.
- context-mode = use the sandbox MCP server: `ecommerceapp-context-mode`.
- context-mode must not invoke RAG or any `mcp__rag*` tool; if RAG is needed, do it as a separate step outside context-mode.
- Knowledge intent -> RAG first.
- Analysis/execution intent -> context-mode first.
- If RAG is empty/unavailable, use context-mode on local files/snippets before classic tools.
- Classic tools are last resort only when both RAG and context-mode fail.
- For implementation tasks, start with bounded context-mode probing before classic repo reads.
- For derived results, use RAG for source retrieval and context-mode for computation/generation.
- Users do NOT need to name RAG or context-mode explicitly. Infer intent from the task shape, target files, and requested outcome.
- If the user asks about repo docs, ADRs, known issues, project state, roadmap, or configuration meaning, route to RAG even when they only say "sprawdź", "wyjaśnij", "dlaczego", or "co to znaczy".
- If the user asks to analyze, compare, count, transform, derive, inspect logs, or validate behavior from local workspace evidence, route to context-mode even when they do not mention the sandbox.
- If the user asks to change code or design implementation details, begin with bounded context-mode probing on the smallest relevant files, then patch exact bytes with classic tools only if needed.

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

Definition: context-mode is the local sandbox for thinking in code. Use it to read local files/snippets, search indexed session data, compute reductions, compare outputs, generate code fragments, and turn repo facts into a concrete result. It is not the place for classic repo browsing or final file editing; it is the place to derive the answer before you touch files.

Use context-mode first when the task is analyze/summarize/count/compare/parse/transform/extract.

For implementation tasks, start with a bounded context-mode probe on the target files/snippets before using classic repo reads. `read_file` / `grep_search` are allowed only after context-mode returns no useful signal or when you need exact bytes for a final edit.

Implementation expectation: first ask context-mode to produce the draft result from the smallest useful evidence set, then use classic tools only to patch exact bytes if necessary. Do not let the flow become read_file-first just because the task is coding.

If the user wants a derived result, code generation, numeric output, table, or transformation based on repo knowledge, use RAG only to retrieve the source material and use context-mode to do the work. In practice: fetch facts with RAG, then compute or generate with `ctx_execute` / `ctx_execute_file` / `ctx_batch_execute`, and store the useful result with `ctx_index` when it should be reused later.

Default working sequence:
1. Use RAG for docs, ADRs, and project knowledge.
2. Use context-mode for any derived result, analysis, math, code generation, or transformation.
3. If RAG is unavailable or returns empty/low-signal results, stay in the MCP path and use context-mode on local files / captured snippets to derive the answer.
4. Fall back to classic tools only if both RAG and context-mode fail for the current step.
5. Do not skip straight to classic tools when MCP is available.

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

Explicit tool naming is optional. The agent must infer the right MCP path from the user's intent.

Examples:
- "Analyze these logs" -> context-mode.
- "Count symbol usages" -> context-mode.
- "What does ADR-0029 say" -> RAG.
- "Why does GPT-5 mini skip the rules" -> read the routing instructions and agent decisions first, then diagnose with the narrowest relevant evidence.
- "Sprawdź czy to działa" about docs/configuration -> RAG first.
- "Sprawdź ten log / kod / output" -> context-mode first.

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
