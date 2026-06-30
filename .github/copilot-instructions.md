# Copilot Instructions for ECommerceApp

> Repo-level policy for AI agents. Per-stack details auto-load via `applyTo:` globs. Full routing table → `docs-index.instructions.md`.

## Top Rules

- Start with [agent-workflow.instructions.md](instructions/agent-workflow.instructions.md); it is the short front door for task routing.
- Keep this file focused on repo-specific policy and setup, not duplicated routing prose.

## 1. Project summary

ECommerceApp — ASP.NET Core MVC + Web API e-commerce platform. Clean/onion architecture, EF Core, ASP.NET Core Identity.

**Projects**: `Web` (MVC + Identity), `API` (REST + JWT), `Application`, `Infrastructure` (EF Core, repos), `Domain`, plus unit/integration tests.

**Domain areas**: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies (NBP API), Identity & User Management.

**Tech**: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit, Moq, FluentAssertions, MSSQL. Frontend: Bootstrap, jQuery, require.js, LibMan. UI labels are partially in Polish — do not translate without explicit request.

## 2. Configuration map

`docs-index.instructions.md` is the **single routing table** for all Copilot config (instructions, prompts, agents, skills, ADRs, context files, `AGENT-PIPELINE.md`). Human-facing docs start at `docs/README.md`. `setup-state.md` is the compact current-state snapshot used by maintainer audits; `COPILOT-SETUP-CHANGELOG.md` is archival only and not a sync trigger.

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
- **Sync rule**: After any `.github/` or `docs/` change, invoke `@copilot-setup-maintainer`; use Workflow 7 only when the Copilot inventory or structure changed — see `pre-edit.instructions.md`.

## 5. Communication & PRs

- PRs must explain what changed, why, tests added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` for ADR-impacting PRs.

## 6. Project context (read before implementation)

**Bug fix rule**: Before any bug fix, MUST read `known-issues.md`.

**Test rule**: Before adding skip/xfail to any test, MUST read `test-stabilization-policy.md`. Every skip needs a tracking ref (KI-NNN or issue #).

**Agent memory rule**: Skim `agent-decisions.md` before non-trivial work — auto-loaded via `agent-memory.instructions.md`. Append after every meaningful correction (see `pre-edit.instructions.md`).

**Clarification rule**: If scope, BC ownership, blocker status, or destructive target are unclear, ask BEFORE writing code. Triggers + host-aware mechanism (`vscode_askQuestions` in VS Code, plain numbered-list chat reply in Visual Studio / other hosts) live in [pre-edit.instructions.md §Clarification policy](instructions/pre-edit.instructions.md).

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

Canonical source: [.github/instructions/mcp-routing.instructions.md](instructions/mcp-routing.instructions.md) (`applyTo: **` — auto-loads every session). All rules, tables, retry sequences, anti-patterns, and the Invalid-answer directive live there.

Canonical maintenance rule: keep full routing logic in the canonical file above. This root section stays compact (summary + pointers) to prevent configuration drift.

Non-negotiable summary:

- **Core precedence is mandatory**: knowledge intent → RAG, execution/analysis intent → context-mode, project URLs → `ctx_fetch_and_index`, never both MCPs for one atomic intent.
- **Intent inference is mandatory**: do not wait for the user to name a tool. Infer RAG vs context-mode from the task shape and target files.
- **Context-mode definition**: the local sandbox for thinking in code. Use it to read local files/snippets, search indexed session data, compute reductions, compare outputs, generate code fragments, and turn repo facts into a concrete result before touching files.
- **Derived-result rule is mandatory**: if you need code, math, a table, a transformed dataset, or a summary generated from repo knowledge, first retrieve the source with RAG if needed, then do the derivation with `ctx_execute` / `ctx_batch_execute` / `ctx_execute_file`, and cache reusable outputs with `ctx_index`.
- **RAG-fail fallback rule is mandatory**: if RAG is empty/unavailable, do not jump straight to classic tools; use context-mode on local files or captured snippets first, and only then fall back to classic tools if the MCP path still fails.
- **Context-mode first probe is mandatory**: for implementation tasks, start with bounded context-mode probing on the target files/snippets before using classic repo reads; `read_file` / `grep_search` are only allowed after context-mode returns no useful signal or when exact bytes are needed for the final edit.
- **First-move discipline is mandatory**: no `read_file`/`grep_search` first on protected knowledge paths when RAG should answer.
- **Integrity and resilience are mandatory**: `ctx_stats` is KPI source of truth; canceled calls require retry/fallback/partial reporting.
- **Long-wait default is mandatory**: for potentially long MCP calls, use a 5-minute threshold (`timeout=300000` where supported) before treating the step as canceled.
- **Retry contract is mandatory**: after cancel/timeout, retry with lighter shape up to 5 times; do not repeat the same command shape verbatim.
- **Runtime default is mandatory**: for context-mode processing, default to `javascript`; any non-`javascript` runtime requires availability verification first, then fallback to `javascript`/bounded `shell` if unavailable.
- **End-of-run telemetry is mandatory**: if any `ctx_*` tool was used, include raw `ctx_stats` in the final answer (unless user explicitly asks to skip metadata).
- **Graceful degradation is mandatory**: when a canceled step cannot be recovered, emit explicit inability (`UNABLE_TO_PROCESS` + reason), mark run `PARTIAL`, and continue remaining independent steps.
- **Known-bad shape guard is mandatory**: do not dispatch known cancellation-prone unbounded shell scans to context-mode; short-circuit with explicit inability and continue using safe rewritten shape.
- **Path and retry safety are mandatory**: `ctx_execute_file` path normalization plus empty-RAG retry sequence before fallback.

Operational details and exact wording live in canonical sections:

- Precedence + first-move restrictions: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md)
- Invalid-answer + empty-result sequence: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md)
- Benchmark integrity + telemetry: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md)
- Canceled recovery + anti-patterns: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md)
- Path normalization + runtime defaults: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md)

Enforced by [.github/context/anti-patterns-critical.context.md](context/anti-patterns-critical.context.md). RAG re-index rules: [instructions/rag.instructions.md](instructions/rag.instructions.md).

## 13. RAG HTTP error envelope

All RAG HTTP endpoints (both .NET and Python HTTP/HTTP servers) return errors as a sanitised JSON envelope:

```json
{ "error": "<safe message>", "code": "<bucket>" }
```

Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`, `InternalServerError`. Stack traces and absolute filesystem paths are never returned to the client — they are logged server-side only. When adding a new endpoint or tool, do not bypass `ApiExceptionHandler` / `BadRequestEnvelopeMiddleware` (.NET) or the Starlette global handlers / `_sanitize_error_message` (Python). Tool methods on `[McpServerTool]` classes must call `McpToolGuard.RunAsync(...)`; Python `@server.call_tool()` handlers must stay inside the existing try/except guard. See [docs/rag/rag-architecture.md §14](../docs/rag/rag-architecture.md#14-error-handling-sanitisation-and-middleware).

## 14. Batched-tasks auto-detection

Auto-loads via [.github/instructions/batched-tasks.instructions.md](instructions/batched-tasks.instructions.md) (`applyTo: **`). Detection patterns, output shape, eval-mode delegation, compact mode, and negative triggers live there. Full rules: [.github/prompts/batched-tasks.prompt.md](prompts/batched-tasks.prompt.md).

## 15. Context budget and Progressive Disclosure

> Target: ≤ 8,000 input tokens per turn. Fixed always-loaded block = ~7,110 tokens. See [.github/context-cost-analysis.md](context-cost-analysis.md) for full measurements.

Load context progressively — never bulk-load everything:

| Tier | What to load | When |
|---|---|---|
| **Tier 0** | This file (auto) + `applyTo: "**"` instructions | Every turn — no action needed |
| **Tier 1** | ONE context file or ADR matching the task | Most routine tasks |
| **Tier 2** | Relevant ADR(s) from `docs/adr/` via `get_history(id)` | Changes that touch governed patterns |
| **Tier 3** | `dotnet.instructions.md` (4,540 tokens) | Deep architectural questions or full code reviews only |

**Navigation Map** — task to context file or agent:

| Task | Invoke | Load |
|---|---|---|
| Fast pre-commit check (BLOCKS MERGE only) | `.github/skills/code-validator` | `anti-patterns-critical.context.md` |
| Full pre-PR code review | `@code-reviewer` or `/pr-review` | auto — conditional per changed stack |
| General codebase question | `/general` | only the relevant ADR or context file |
| Distill domain concepts / find safe generalizations | `@context-distiller` | domain description / event storming output |
| Create / update flow specification | `@spec-writer` | `specification.template.md` + matching ADR |
| Add CQRS command + handler | skill `create-cqrs-handler` | `bc-adr-map.instructions.md` |
| Scaffold BC DbContext or DI extension | skill `create-dbcontext` / `create-di-extension` | `efcore.instructions.md` |
| Add EF Core entity type config | skill `create-ef-configuration` | `efcore.instructions.md` |
| Add cross-BC event / message | skill `create-domain-event` / `create-message-contract` | `dotnet.instructions.md` §Messaging |
| Scaffold unit test | skill `create-unit-test` | `anti-patterns-critical.context.md` |
| Scaffold integration test | skill `create-integration-test` | `anti-patterns-critical.context.md` |
| Generate Mermaid diagram | skill `mermaid-diagram` | (self-contained) |
| Sync context files after ADR change | skill `context-updater` | source-of-truth table in skill |
| Analyze BC flows (bidirectional) | `/flow-analysis` | matching ADR + `anti-patterns-critical.context.md` |
| Refactor (structural only) | `/refactor` | `agent-decisions.md` + target file(s) |

**Proactive rule**: when a task matches a row above, load and follow that skill or agent automatically — you do not need to be explicitly asked.
