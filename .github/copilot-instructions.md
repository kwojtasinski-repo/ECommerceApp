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
- **First-move discipline is mandatory**: no `read_file`/`grep_search` first on protected knowledge paths when RAG should answer.
- **Integrity and resilience are mandatory**: `ctx_stats` is KPI source of truth; canceled calls require retry/fallback/partial reporting.
- **End-of-run telemetry is mandatory**: if any `ctx_*` tool was used, include raw `ctx_stats` in the final answer (unless user explicitly asks to skip metadata).
- **Graceful degradation is mandatory**: when a canceled step cannot be recovered, emit explicit inability (`UNABLE_TO_PROCESS` + reason), mark run `PARTIAL`, and continue remaining independent steps.
- **Known-bad shape guard is mandatory**: do not dispatch known cancellation-prone unbounded shell scans to context-mode; short-circuit with explicit inability and continue using safe rewritten shape.
- **Path and retry safety are mandatory**: `ctx_execute_file` path normalization plus empty-RAG retry sequence before fallback.

Operational details and exact wording live in canonical sections:

- Precedence + first-move restrictions: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md#hard-precedence-rules-apply-in-this-order-no-exceptions)
- Invalid-answer + empty-result sequence: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md#invalid-answer-directive)
- Benchmark integrity: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md#benchmark-integrity-rule-ctx_stats)
- Canceled recovery + anti-patterns: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md#tool-cancel-recovery-rule-canceled)
- Path normalization: [mcp-routing.instructions.md](instructions/mcp-routing.instructions.md#context-mode-path-normalization-mandatory)

Enforced by [.github/context/anti-patterns-critical.context.md](context/anti-patterns-critical.context.md). RAG re-index rules: [instructions/rag.instructions.md](instructions/rag.instructions.md).

## 13. RAG HTTP error envelope

All RAG HTTP endpoints (both .NET and Python HTTP/HTTP servers) return errors as a sanitised JSON envelope:

```json
{ "error": "<safe message>", "code": "<bucket>" }
```

Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`, `InternalServerError`. Stack traces and absolute filesystem paths are never returned to the client — they are logged server-side only. When adding a new endpoint or tool, do not bypass `ApiExceptionHandler` / `BadRequestEnvelopeMiddleware` (.NET) or the Starlette global handlers / `_sanitize_error_message` (Python). Tool methods on `[McpServerTool]` classes must call `McpToolGuard.RunAsync(...)`; Python `@server.call_tool()` handlers must stay inside the existing try/except guard. See [docs/rag/rag-architecture.md §14](../docs/rag/rag-architecture.md#14-error-handling-sanitisation-and-middleware).

## 14. Batched-tasks auto-detection

Auto-loads via [.github/instructions/batched-tasks.instructions.md](instructions/batched-tasks.instructions.md) (`applyTo: **`). Detection patterns, output shape, eval-mode delegation, compact mode, and negative triggers live there. Full rules: [.github/prompts/batched-tasks.prompt.md](prompts/batched-tasks.prompt.md).

