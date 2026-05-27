# Anti-Patterns — Critical (BLOCKS MERGE)

> Any violation below **blocks merge** — must be fixed before a PR is merged.
> Consolidated from: `safety.instructions.md`, `dotnet.instructions.md`, `project-state.md`, `shared-primitives.instructions.md`, `efcore.instructions.md`.

_Last updated: 2026-03-17_

---

## Architecture violations

- **No business logic in controllers** — controllers are thin; delegate all logic to application services.
- **No `Infrastructure` references in `Application`** — application layer must not reference Infrastructure directly.
- **No external project references in `Domain`** — domain layer depends on nothing.
- **No EF Core entities or DbContext in `Application`/`Web`/`API`** — keep EF Core in Infrastructure.
- **No direct service-to-service calls across BC boundaries** — use `IMessageBroker` (in-memory) only.
- **No `ApplicationUser` navigation properties** — domain models use `string UserId` only.

## Exception handling

- **No raw `try/catch` in controllers** — exceptions flow through `ExceptionMiddleware` → `IErrorMapToResponse`.
- **No `return BadRequest(ex.Message)` in controllers** — use `BusinessException` pipeline.
- **No controller-level exception handling without `MapExceptionAsRouteValues()`** helper.

## Domain model

- **No public setters on behavioral aggregates** (`Order`, `Payment`, `Refund`, `OrderItem`) — use `private set`.
- **No external state mutation** — state changes go through named domain methods on the aggregate.
- **No Law of Demeter violations** — do not chain through navigation properties; pass values as params.
- **No raw `int`/`Guid`/`string` as entity IDs in domain models** — use `TypedId<T>`.
- **No new shared primitives without ADR-0006 update**.

## Services & repositories

- **No standalone CRUD service classes bypassing `AbstractService`** — extend it for standard CRUD.
- **No duplication of handler logic** in controllers, services, or repositories.
- **No exposing `DbContext` or `IQueryable` outside Infrastructure**.
- **No `IQueryable<T>` returned to service layer** — compose queries in repository methods.
- **No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`** — always `async`/`await`.

## DI & registration

- **No service registration in `Startup.cs`/`Program.cs`** — use extension methods in each layer's `DependencyInjection.cs`.

## Security

- **No direct `HttpContext.User` / `User.Claims` access in controllers** — use `GetUserId()`/`GetUserRole()` from `BaseController`.
- **No bypassing `[Authorize]` attributes or role checks** without explicit sign-off.
- **No hardcoded role strings** — use `ManagingRole`, `MaintenanceRole` constants from `BaseController`.
- **No secrets or credentials committed** — ever.

## Testing

- **No bare `Assert.*`** — use FluentAssertions (unit) or Shouldly (API integration).
- **No manual `IHttpContextAccessor` mocking in integration tests** — use `SetHttpContextUserId()` from `BaseTest<T>`.
- **No renaming legacy `given_when_should` tests** — only new tests use `Method_Conditions_ExpectedResult`.

## Frontend

- **No global functions** — register modules via `require.js`.
- **No direct AJAX calls** — use `ajaxRequest` helper for consistent headers/CSRF.
- **No Polish text changes without product/team approval**.

## Files & operations

- **No edits to `Infrastructure/Migrations/` files** without explicit human approval.
- **No production DB migrations** without explicit human approval.
- **No preview SDK or preview major package upgrades** without confirmation.

## MCP routing

- **No calling both RAG and context-mode MCPs for the same atomic intent** — pick one per intent, per [mcp-routing.instructions.md](../instructions/mcp-routing.instructions.md). Double-calls inflate context and contradict the precedence rules.
- **No `grep_search` or `read_file` on `.github/context/*.md`, `docs/adr/**`, `docs/roadmap/**`, or `docs/architecture/bounded-context-map.md` BEFORE `query_docs`/`get_history`** — these paths are RAG-owned. Direct file access is a fallback only after the MCP returns empty or low-score. Treating a known-issue / project-state / agent-decisions / ADR question as a code search is the #1 routing failure mode.
- **No reporting "RAG returned empty" without first executing the mandatory retry sequence** — when `query_docs`/`read_docs` returns empty or low-score, you MUST (1) retry WITHOUT `bc=`, then (2) retry with REWORDED keywords using full-name domain synonyms (NOT literal IDs). Only after both retries fail may you state empty and fall back. Skipping either retry is the failure mode that broke Q5 (KI-008) in the routing eval — model gave up after one literal `query_docs("KI-008")` instead of retrying as `query_docs("FluentAssertions AwesomeAssertions .NET 8")` which would have hit. Equally forbidden: filling the empty result with training-memory inference. Full rule in [mcp-routing.instructions.md](../instructions/mcp-routing.instructions.md#empty-result-clause).
- **No raw `fetch_webpage` for project-related URLs** (ADRs, docs, package registries, GitHub links cited in repo work) — must go through `ctx_fetch_and_index` (ADR-0029). Bypasses the AdGuard allowlist.
- **No quoting ADRs / project state / known issues / roadmap from training data** — always look up via RAG (`get_history`, `query_docs`). Stale memory is a frequent cause of wrong answers.
- **No computing hashes, math, regex transformations, or repo-wide derivations from training-data memory** — use `ctx_execute` / `ctx_execute_file` in the sandbox. The bytes never enter context; the answer is verifiable.
- **No MCP tool calls inside `@verifier` or any deterministic gate** — breaks reproducibility.

## Legacy code (do not extend)

These classes are frozen — all new work goes to their BC replacements:

| Legacy file                                          | New BC equivalent                                               |
| ---------------------------------------------------- | --------------------------------------------------------------- |
| `Application/Services/Orders/OrderService.cs`        | `Application/Sales/Orders/Services/OrderService.cs`             |
| `Application/Services/Payments/PaymentService.cs`    | `Application/Sales/Payments/Services/PaymentService.cs`         |
| `Application/Services/Customers/CustomerService.cs`  | `Application/AccountProfile/Services/UserProfileService.cs`     |
| `Application/Services/Currencies/CurrencyService.cs` | `Application/Supporting/Currencies/Services/CurrencyService.cs` |
| `Domain/Model/` (anemic models)                      | BC-specific aggregates under `Domain/<BC>/`                     |
