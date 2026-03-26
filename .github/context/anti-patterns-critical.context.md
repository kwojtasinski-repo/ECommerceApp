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

## Legacy code (do not extend)

These classes are frozen — all new work goes to their BC replacements:

| Legacy file                                          | New BC equivalent                                               |
| ---------------------------------------------------- | --------------------------------------------------------------- |
| `Application/Services/Orders/OrderService.cs`        | `Application/Sales/Orders/Services/OrderService.cs`             |
| `Application/Services/Payments/PaymentService.cs`    | `Application/Sales/Payments/Services/PaymentService.cs`         |
| `Application/Services/Customers/CustomerService.cs`  | `Application/AccountProfile/Services/UserProfileService.cs`     |
| `Application/Services/Currencies/CurrencyService.cs` | `Application/Supporting/Currencies/Services/CurrencyService.cs` |
| `Domain/Model/` (anemic models)                      | BC-specific aggregates under `Domain/<BC>/`                     |
