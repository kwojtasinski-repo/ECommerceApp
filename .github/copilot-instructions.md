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

## 12. RAG / MCP tool routing

The project exposes four MCP tools via `ecommerceapp-rag-dotnet` (or any other configured variant).
**Route questions to the correct tool** — do not guess from training data for questions that have a definitive answer in the project docs.

| Trigger phrase / intent | Tool to call | When to use |
|---|---|---|
| "list ADRs", "what ADRs exist", "show all decisions" | `list_adrs` | Enumerate all indexed ADRs |
| "ADR-NNNN", "ADR about X", "decision on X" | `get_history(id="NNNN")` | Retrieve all indexed chunks for a specific ADR |
| General architecture / pattern / "how does X work?" | `query_docs(query="...")` | Semantic search over all docs |
| "full content of file X", "show me everything about X", "all details" | `read_docs(query="...")` | Returns full file content for top match |
| Known issues, bug fixes, blocked BCs, project state | `query_docs` → target `.github/context/` chunks | High-relevance context files are weighted 1.15–1.25 |

**Rules:**
- Always use a MCP tool before answering questions about ADRs, project state, known issues, or roadmap — never guess from training data.
- If the tool returns "No chunks found", fall back to saying so and suggest re-running ingest.
- Prefer `get_history` over `query_docs` when the user mentions a specific ADR number or title.
- Both Python and .NET MCP implementations expose identical tool names — routing is the same regardless of which server variant is enabled.
- **Multi-MCP precedence** (RAG + context-mode coexistence): knowledge → RAG; sandboxed execution / file summarisation → context-mode; external URL → `ctx_fetch_and_index` only (never raw `fetch_webpage`); both empty → direct `read_file` / `grep_search`. **NEVER call both MCPs for the same atomic intent.** Full rules + ASCII flow: [docs/rag/mcp-first-routing-migration-playbook.md §13](../docs/rag/mcp-first-routing-migration-playbook.md#13-coexistence-with-a-second-mcp-server-worked-example-context-mode). Per-tool routing: [.github/instructions/rag.instructions.md](instructions/rag.instructions.md).

## 13. RAG HTTP error envelope

All RAG HTTP endpoints (both .NET and Python HTTP/HTTP servers) return errors as a sanitised JSON envelope:

```json
{ "error": "<safe message>", "code": "<bucket>" }
```

Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`, `InternalServerError`. Stack traces and absolute filesystem paths are never returned to the client — they are logged server-side only. When adding a new endpoint or tool, do not bypass `ApiExceptionHandler` / `BadRequestEnvelopeMiddleware` (.NET) or the Starlette global handlers / `_sanitize_error_message` (Python). Tool methods on `[McpServerTool]` classes must call `McpToolGuard.RunAsync(...)`; Python `@server.call_tool()` handlers must stay inside the existing try/except guard. See [docs/rag/rag-architecture.md §14](../docs/rag/rag-architecture.md#14-error-handling-sanitisation-and-middleware).

