# Copilot Instructions for ECommerceApp

> Repo-level policy for AI agents. Per-stack details auto-load via `applyTo:` globs. Full routing table → `docs-index.instructions.md`.

## 1. Project summary

ECommerceApp — ASP.NET Core MVC + Web API e-commerce platform. Clean/onion architecture, EF Core, ASP.NET Core Identity.

**Projects**: `Web` (MVC + Identity), `API` (REST + JWT), `Application`, `Infrastructure` (EF Core, repos), `Domain`, plus unit/integration tests.

**Domain areas**: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies (NBP API), Identity & User Management.

**Tech**: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit, Moq, FluentAssertions, MSSQL. Frontend: Bootstrap, jQuery, require.js, LibMan. UI labels are partially in Polish — do not translate without explicit request.

## 2. Configuration map

`docs-index.instructions.md` is the **single routing table** for all Copilot config (instructions, prompts, agents, skills, ADRs, context files, `AGENT-PIPELINE.md`). Human-facing docs start at `docs/README.md`.

## 3. AI developer profile

- Act as a senior .NET developer experienced with DDD, SOLID, and pragmatic TDD.
- Be concise and technical; prefer code-first responses.
- Ask clarifying questions when requirements are ambiguous.
- Always add or update tests for behavioral changes.
- **Multi-option rule**: For architectural decisions (new pattern, BC pattern choice, infra change), propose 2–5 approaches and ask human to choose before implementing. Skip for trivial/mechanical changes.

## 4. Key rules (do not bypass)

- New ADR → copy `adr.template.md` → `docs/adr/XXXX/XXXX-short-title.md` + `docs/adr/XXXX/README.md` router.
- Read applicable per-stack instructions before writing code for that stack.
- Detailed rules for AbstractService, Handler pattern, ExceptionMiddleware, IFileStore, NBP API → `dotnet.instructions.md`.
- **BC changes rule**: Before editing BC code, MUST read `project-state.md`. If blocked, STOP. Atomic switches deferred until 80–95% migration complete.
- **Feed-forward rule**: When docs/ADR meaning changes, update `.github` in the same task.
- **Sync rule**: After any `.github/` or `docs/` change, invoke `@copilot-setup-maintainer` (Workflow 11 + 7 minimum) — see `pre-edit.instructions.md`.

## 5. Communication & PRs

- PRs must explain what changed, why, tests added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` for ADR-impacting PRs.

## 6. Project context (read before implementation)

**Bug fix rule**: Before any bug fix, MUST read `known-issues.md`.

**Test rule**: Before adding skip/xfail to any test, MUST read `test-stabilization-policy.md`. Every skip needs a tracking ref (KI-NNN or issue #).

**Agent memory rule**: Skim `agent-decisions.md` before non-trivial work — auto-loaded via `agent-memory.instructions.md`. Append after every meaningful correction (see `pre-edit.instructions.md`).

**Clarification rule**: If scope, BC ownership, or blocker status are unclear, ask BEFORE writing code.

Context: `project-state.md`, `known-issues.md`, `repo-index.md`. Roadmaps: `docs/roadmap/README.md`. BC map: `bounded-context-map.md`.

**Architecture suggestion rule**: Follow `pre-edit.instructions.md` triggers for ADR, BC map, roadmap, or project-state updates.

## 7. BC → ADR quick map

Loaded automatically when editing `.cs`/`.csproj`/`.cshtml` via `bc-adr-map.instructions.md`. Full routing table in `docs-index.instructions.md`.

## 8. Coupons

- Max coupons/order: default 5, ceiling 10 (`CouponsOptions.MaxCouponsPerOrder`). See ADR-0016.

## 9. .NET 8+ Upgrade

- Replace `FluentAssertions` → `AwesomeAssertions` on .NET 8+ upgrade (drop-in, no syntax changes). Do NOT on .NET 7. See [KI-008](context/known-issues.md).

## 10. API Purchase Limits

- Max 5 units/line via `MaxApiQuantityFilter` (`ApiPurchaseOptions`); Web max 99 (`AddToCartDtoValidator`). Never cap `Shared.Quantity`.
- `TrustedApiUser` = authenticated + `api:purchase` claim OR `Service`/`Manager`/`Administrator` role.

## 11. Flow analysis

When asked to **analyze** a user-facing flow, trace it in **both directions**:
- **Start → End**: happy path + every failure branch
- **End → Start**: verify every state has a valid predecessor, all guards exist, no dead ends

Use `#file:.github/prompts/flow-analysis.prompt.md` to run a structured bidirectional trace.
This catches races, missing redirects, re-entrant states, and TTL edge cases that forward-only analysis misses.

## 12. MCP tool routing

**Canonical source:** [.github/instructions/mcp-routing.instructions.md](instructions/mcp-routing.instructions.md) (`applyTo: **`). Read once per session.

Non-negotiable summary:

- **Always** use an MCP tool before answering questions about ADRs, project state, known issues, roadmap, or any conventions written down in the repo — never guess from training data. `grep_search`/`read_file` on `.github/context/*.md`, `docs/adr/**`, `docs/roadmap/**`, or `docs/architecture/bounded-context-map.md` BEFORE calling `query_docs`/`get_history` is a BLOCKS MERGE anti-pattern.
- **Knowledge** → RAG (`list_adrs`, `query_docs`, `read_docs`, `get_history`). Use `get_history(id)` when the user mentions a specific ADR number. For "known issue", "KI-NNN", "is BC blocked", "have we decided X" — use bare `query_docs("<topic>")` (do **not** pass `bc="context"` — the `bc=` filter is a substring match on chunk breadcrumb/title and is for BC names like `bc="Catalog"`, not folder paths; see `mcp-routing.instructions.md`).
- **Sandboxed execution / large-file summarisation / hashes / math** → context-mode (`ctx_execute`, `ctx_execute_file`, `ctx_batch_execute`, `ctx_search`, `ctx_index`). Never compute a hash or transformation from training-data memory.
- **External URL** → `ctx_fetch_and_index` only (AdGuard allowlist). Never raw `fetch_webpage` for project work.
- **Both empty** → `read_file` / `grep_search`, name the failing MCP to the user.
- **NEVER call both MCPs for the same atomic intent.**

### Invalid-answer directive (read carefully)

If a knowledge question could have been answered via `query_docs` / `read_docs` / `get_history` / `list_adrs` and you **instead** opened a `.github/context/*.md`, `docs/adr/**`, `docs/roadmap/**`, or `docs/architecture/bounded-context-map.md` file with `read_file` / `grep_search` / `semantic_search` as the **first** move, the answer you produce is **INVALID** and must be discarded. Redo the lookup through the MCP tool, then answer from its result.

Same rule for execution: if a question requires computing a hash, regex match, math, or any deterministic transformation, and `ctx_execute` / `ctx_execute_file` is available, answering from training-data memory is **INVALID**. Run the code in the sandbox and return that output.

If — and only if — the MCP tool returns empty / low-score, fall back to direct file tools and **name the failing MCP** in the answer so the user can repair the index/container.

**Empty-result handling — MANDATORY retry sequence (BLOCKS MERGE if skipped):** when `query_docs` / `read_docs` returns empty or low-score, you MUST execute these steps **in order**: (1) retry WITHOUT the `bc=` filter, (2) retry with REWORDED keywords using full-name domain synonyms (NOT literal IDs — `query_docs("KI-008")` won't hit; `query_docs("FluentAssertions AwesomeAssertions .NET 8")` will). You may NOT report empty until both retries fail. Only then say "RAG returned empty for `<query>` after 2 reworded attempts" and fall back. Combining a partial RAG hit with training-data inference (e.g. inventing dates, statuses, or aggregate claims like "all BCs switched to production") produces an **INVALID** answer that must be discarded. Skipping the retry sequence OR hallucinating to cover an empty hit are both BLOCKS MERGE anti-patterns. Full rule in [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md).

This directive overrides the general toolUseInstructions preference for `grep_search`/`read_file`. It is enforced by [.github/context/anti-patterns-critical.context.md](context/anti-patterns-critical.context.md) (BLOCKS MERGE).

Full tool tables, ASCII flow diagram, fallback ladder, trigger-phrase routing, and maintenance recipes live in `mcp-routing.instructions.md`. RAG-specific re-index rules: [instructions/rag.instructions.md](instructions/rag.instructions.md).

## 13. RAG HTTP error envelope

All RAG HTTP endpoints (both .NET and Python HTTP/HTTP servers) return errors as a sanitised JSON envelope:

```json
{ "error": "<safe message>", "code": "<bucket>" }
```

Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`, `InternalServerError`. Stack traces and absolute filesystem paths are never returned to the client — they are logged server-side only. When adding a new endpoint or tool, do not bypass `ApiExceptionHandler` / `BadRequestEnvelopeMiddleware` (.NET) or the Starlette global handlers / `_sanitize_error_message` (Python). Tool methods on `[McpServerTool]` classes must call `McpToolGuard.RunAsync(...)`; Python `@server.call_tool()` handlers must stay inside the existing try/except guard. See [docs/rag/rag-architecture.md §14](../docs/rag/rag-architecture.md#14-error-handling-sanitisation-and-middleware).

## 14. Batched-tasks auto-detection

If the user's message contains a **list of 3 or more discrete actionable items** \u2014 questions, tasks, or mixed \u2014 treat it as a **batched task list** and apply the rules + adaptive structured output format from [.github/prompts/batched-tasks.prompt.md](prompts/batched-tasks.prompt.md) automatically. Do NOT preamble. Do NOT ask for confirmation. Output begins with the first item's prefix.

**Detection patterns** (any of these triggers batch mode):

| Pattern                                            | Example                                          |
| -------------------------------------------------- | ------------------------------------------------ |
| 3+ `Q<N>.` markers                                 | `Q1. ... Q2. ... Q3. ...`                        |
| 3+ numbered items (`1.` `2.` `3.` or `1)` `2)` ...) | `1. fix the validator 2. add a test 3. ...`     |
| 3+ bulleted items (`-` or `*`)                     | `- check KI-008\n- list ADRs\n- compute sha256` |
| 3+ `Task <N>:` / `Step <N>:` prefixes              | `Task 1: ... Task 2: ... Task 3: ...`            |
| 3+ separate `?`-ending sentences in one message    | `What is X? What is Y? What is Z?`               |

**Eval-mode delegation:** if the input has `Q<N>.` markers AND the user said any of "eval" / "test these" / "batch test" / "score" / "measure" / "rate" / "grade", delegate to the stricter [.github/prompts/mcp-routing-eval.prompt.md](prompts/mcp-routing-eval.prompt.md) instead (adds `CODE STRING:` and `RETRY TRACE:` requirements and forbids markdown headings entirely).

**Output format adapts to the input style.** `Q<N>.` input \u2192 `Q<N>:` output blocks. Numbered input \u2192 `<N>:` blocks. Bulleted input \u2192 `- Item <N>:` blocks. Per-item shape: `TOOL USED:` / `ANSWER:` / `CONFIDENCE:` / optional `NOTE:`. Full table in the prompt file.

**Compact mode:** if the user adds "fast" / "quick" / "short" / "no metadata" to the message, output one line per item (`<prefix> <answer>`), skipping the metadata fields. MCP routing rules still apply silently.

**Negative triggers** (do NOT activate batch mode):
- Fewer than 3 items.
- A single question that happens to contain a numbered example list inside it.
- A long-form essay / documentation request.
- Genuine multi-turn troubleshooting where each step depends on the previous response.

Full rules, edge cases, and forbidden patterns: [.github/prompts/batched-tasks.prompt.md](prompts/batched-tasks.prompt.md).

