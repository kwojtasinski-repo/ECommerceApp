---
description: ".NET development guidelines for ECommerceApp. Covers architecture rules, EF Core, repositories, services, handlers, exception handling, testing, and async patterns."
applyTo: "**/*.cs, **/*.csproj"
---

# .NET Development Guidelines for ECommerceApp

> Always read this file before writing or modifying any C# code in this repository.

## 1. Architecture overview

The solution follows clean/onion architecture with strict layer boundaries:

- `ECommerceApp.Domain` — domain models (`BaseEntity` subclasses) and repository interfaces (`IGenericRepository<T>`). No dependencies on other layers.
- `ECommerceApp.Application` — services, handlers, DTOs, ViewModels, validators, AutoMapper profiles, middleware, exceptions. Depends only on `Domain`.
- `ECommerceApp.Infrastructure` — EF Core `Context`, `GenericRepository<T>`, concrete repositories, auth, migrations, seed data. Depends on `Application` and `Domain`.
- `ECommerceApp.Web` — MVC controllers, views, filters, `Startup.cs`. Thin UI layer; depends on `Application`.
- `ECommerceApp.API` — Web API controllers, JWT auth, `Program.cs`. Thin API layer; depends on `Application`.

**Strict rules — never bypass:**
- Controllers (Web + API) must be thin: delegate all logic to application services. No business logic in controllers.
- Application layer must not reference `Infrastructure` directly.
- Domain layer must not reference any other project.
- Do not move EF Core entities or DbContext references into `Application` or `Web`/`API` layers.

## 2. Services — AbstractService base class

All application services that perform CRUD over a single aggregate must inherit from `AbstractService<TVm, URepo, EEntity>`:

```csharp
public class BrandService : AbstractService<BrandVm, IBrandRepository, Brand>, IBrandService
{
    public BrandService(IBrandRepository repo, IMapper mapper) : base(repo, mapper) { }
}
```

- `AbstractService` provides: `Add`, `Delete`, `Get`, `Update` using `_repo` and `_mapper`.
- Do NOT create standalone service classes that bypass `AbstractService` for standard CRUD operations.
- Services with complex logic beyond CRUD (e.g., `OrderService`, `ItemService`) extend or compose `AbstractService` with additional methods.

## 3. Handler pattern

Complex domain operations that span multiple aggregates or require coordinated persistence use the **Handler pattern**:

- `CouponHandler` (`ICouponHandler`) — manages coupon assignment, removal, and cost recalculation on orders.
- `PaymentHandler` (`IPaymentHandler`) — manages payment lifecycle changes.
- `ItemHandler` (`IItemHandler`) — manages item-level operations involving images, tags, brands, types.

Rules:
- Do NOT duplicate handler logic in controllers, services, or repositories.
- Handlers are registered in DI and injected into services that need them.
- Handlers throw `BusinessException` for all domain violations.

## 4. Exception handling

All exceptions must flow through `ExceptionMiddleware` → `IErrorMapToResponse` pipeline:

- Throw `BusinessException` (from `ECommerceApp.Application.Exceptions`) for all domain and validation errors.
- `BusinessException` supports: message, `ErrorCode` (with `ErrorParameter`), and `ErrorMessage` builder for composite errors.
- Do NOT add raw `try/catch` blocks in controllers or services unless you are re-throwing as `BusinessException`.
- `ExceptionMiddleware` catches all unhandled exceptions, logs them, and maps them to HTTP responses via `IErrorMapToResponse`.

```csharp
// Correct
throw new BusinessException($"Brand with id '{id}' was not found",
    ErrorCode.Create("brandNotFound", ErrorParameter.Create("id", id)));

// Wrong — never do this in controllers
try { ... } catch (Exception ex) { return BadRequest(ex.Message); }
```

## 5. BaseController — Web and API

Both `ECommerceApp.Web` and `ECommerceApp.API` have a `BaseController` that all controllers must inherit from:

- `Web.BaseController` extends `Controller` and provides:
  - `GetUserId()` and `GetUserRole()` — read identity claims; never access `User.Claims` directly in action methods.
  - `ManagingRoles`, `MaintenanceRoles` — shared role arrays for `[Authorize(Roles = ...)]` attributes.
  - `MapExceptionToResponseStatus()` / `MapExceptionAsRouteValues()` — map `BusinessException` to route values or status codes for Web redirect flows. Use these when catching exceptions that need to surface as redirect query params.
  - `BuildErrorModel()` helpers — build `ErrorModel`/`NewErrorModel` for consistent error serialization.
- `API.BaseController` extends `ControllerBase` and provides `GetUserId()`, `GetUserRole()`, and role constants.
- Do NOT access `HttpContext.User` or `User.Claims` directly in action methods — use `GetUserId()` / `GetUserRole()` from `BaseController`.
- Do NOT add controller-level try/catch for `BusinessException` without using `MapExceptionAsRouteValues()` helpers.

## 6. Repositories

- All repositories extend `GenericRepository<T>` which provides: `Add`, `AddAsync`, `AddRange`, `Delete`, `DeleteAsync`, `GetById`, `GetByIdAsync`, `GetAll`, `Update`, `UpdateAsync`, `UpdateRange`, `DetachEntity`.
- Repository interfaces live in `Domain/Interface/`. Implementations live in `Infrastructure/Repositories/`.
- Use `AsNoTracking()` for all read-only queries.
- Call `DetachEntity()` after reads when the entity will be re-attached for update to avoid tracking conflicts.
- Prefer `async` variants (`AddAsync`, `DeleteAsync`, `GetByIdAsync`, `UpdateAsync`) for new code.
- Do NOT expose `DbContext` or `IQueryable` outside of `Infrastructure`.
- `GenericRepository.GetAll()` returns `IQueryable<T>` — compose queries in repository methods, do not return raw `IQueryable` to service layer.

## 7. Async and cancellation

- Use `async`/`await` for all I/O, database, and external service calls.
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Prefer `SaveChangesAsync()` over `SaveChanges()` for new code.
- Accept `CancellationToken` in new public async methods where it makes sense.

## 8. AutoMapper and ViewModels / DTOs

- All mapping between domain entities and ViewModels/DTOs is done via AutoMapper.
- Mapping profiles implement `IMapFrom<T>` (from `Application/Mapping/`).
- Do NOT map manually between entities and VMs in services or controllers — always use `_mapper`.
- ViewModels (suffix `Vm`) are used for UI layer (`ECommerceApp.Web`).
- DTOs (suffix `Dto`) are used for API layer (`ECommerceApp.API`) and service input/output.
- Do NOT return domain entities from service methods — always map to VM or DTO.

## 9. Validation

- Input validation uses **FluentValidation**. Validators live in `Application` layer.
- `ModelStateFilter` (in `ECommerceApp.Web/Filters/`) handles MVC model state: returns `400 BadRequest` globally if `ModelState` is invalid — do NOT duplicate this check in action methods.
- Throw `BusinessException` for domain rule violations discovered during service execution (after model-level validation passes).
- Do NOT validate in controllers beyond relying on `ModelStateFilter`.

## 10. File / image handling

- All file operations (image storage) must use `IFileStore` / `IFileWrapper` abstractions from `Application/Interfaces/`.
- Implementations are `FileStore` and `FileWrapper` in `Application/FileManager/`.
- Do NOT use `System.IO` directly for file operations — always go through `IFileStore`/`IFileWrapper`.

## 11. Currency rates — NBP integration

- External currency rates are fetched from the **NBP (National Bank of Poland) API** via `CurrencyRateService` + `NBPResponseUtils`.
- Do NOT hardcode currency rates or bypass `CurrencyRateService`.
- `CurrencyConstants` contains constant values used across currency operations — check it before adding new constants.

## 12. Testing conventions

Unit tests live in `ECommerceApp.UnitTests`, integration tests in `ECommerceApp.IntegrationTests`.

**Test naming — must follow this pattern:**
```
given_<context>_when_<action>_should_<expected_result>()
```

Examples from the codebase:
```csharp
public void given_valid_item_id_should_exists()
public void given_null_item_when_add_item_dto_should_throw_an_exception()
public void given_valid_item_with_images_when_add_item_dto_should_add()
```

**Unit test rules:**
- Extend `BaseTest` (from `UnitTests/Common/BaseTest.cs`) — provides a configured `IMapper` via `MappingProfile`.
- Use `Moq` for mocking dependencies.
- Use `FluentAssertions` for all assertions — never use bare `Assert.*`.
- Use in-memory repository helpers (`GenericInMemoryRepository`, `OrderInMemoryRepository`, `PaymentInMemoryRepository`, `OrderItemInMemoryRepository`) from `UnitTests/Common/` for repository faking.
- Use `UserContextTest` and `HttpContextAccessorTest` from `UnitTests/Common/` when tests require user identity or HTTP context.

**Integration test rules:**
- Extend `BaseTest<TService>` (from `IntegrationTests/Common/BaseTest.cs`) — spins up `CustomWebApplicationFactory<Startup>` and resolves `TService` from DI.
- Use `SetHttpContextUserId(userId)` and `SetUserRole(role)` from `BaseTest<T>` to control the current user identity in tests — do NOT mock `IHttpContextAccessor` manually.
- `Dispose()` in `BaseTest<T>` calls `context.Database.EnsureDeleted()` — always let `Dispose` run; do not suppress it.
- Use `TestDatabaseInitializer` / `DatabaseInitializer` for test DB setup and seed data.
- `PROPER_CUSTOMER_ID` is a shared constant in `BaseTest<T>` — use it when tests require a known valid customer ID.

## 13. DI registration

- Application layer services are registered in `ECommerceApp.Application/DependencyInjection.cs`.
- Infrastructure layer services are registered in `ECommerceApp.Infrastructure/DependencyInjection.cs`.
- Do NOT register services directly in `Startup.cs` / `Program.cs` — use the extension methods from each layer.
- Handlers (`CouponHandler`, `PaymentHandler`, `ItemHandler`) are registered as internal implementations behind their interfaces.

## 14. Security and auth

- Web authentication uses **ASP.NET Core Identity** (cookie-based) with **Google OAuth**.
- API authentication uses **JWT** managed by `JwtManager` (in `Infrastructure/Auth/`).
- Authorization uses role-based access with roles defined in `UserPermissions.Roles`: `Administrator`, `Manager`, `Service`, `User`.
- Role groupings are defined on `BaseController`: `ManagingRole` (Admin + Manager), `MaintenanceRole` (Admin + Manager + Service) — always use these constants in `[Authorize(Roles = ...)]` instead of string literals.
- Do NOT bypass `[Authorize]` attributes or role checks without explicit sign-off.
- `UserContext` (`IUserContext`) provides the current user identity in services — use this instead of accessing `HttpContext.User` directly in services.
- In controllers use `GetUserId()` / `GetUserRole()` from `BaseController`.

## 15. Code style rules

- Match existing naming, formatting, and structure in the target file.
- Prefer small, single-responsibility methods.
- Use C# features available in the project — follow existing file style (no file-scoped namespaces are used; do not introduce them).
- Do NOT add comments unless requested — prefer self-documenting code.
- Do NOT add `sealed` to classes unless specifically required.

## 16. Domain model richness policy

> Strategic rationale: [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../../docs/adr/0002-post-event-storming-architectural-evolution-strategy.md)

The codebase is evolving incrementally from an anemic domain model toward a rich OOP domain model.
Apply the rules below whenever touching or creating domain models in `ECommerceApp.Domain/Model/`.

**Behavioral aggregates** (enforce richness rules): `Order`, `Payment`, `Refund`, `OrderItem`.
**Reference / lookup domains** (CRUD with `AbstractService` remains acceptable): `Brand`, `Tag`, `Type`,
`Currency`, `CurrencyRate`, `ContactDetailType`, `Address`, `Coupon`, `CouponType`.

### Rules for behavioral aggregates

- **Private setters** — all properties must use `private set`. Never expose public setters on behavioral aggregates.
  ```csharp
  // Correct
  public PaymentState State { get; private set; }

  // Wrong
  public PaymentState State { get; set; }
  ```

- **State transitions as methods** — state changes must go through named domain methods on the aggregate.
  Never mutate state from outside (handlers, services, controllers).
  ```csharp
  // Correct — Order owns its transition
  public void MarkAsPaid(int paymentId)
  {
      if (IsPaid) throw new BusinessException("Order already paid");
      IsPaid = true;
      PaymentId = paymentId;
  }

  // Wrong — external mutation from PaymentHandler
  order.IsPaid = true;
  order.PaymentId = paymentId;
  ```

- **Factory methods** — use static factory methods for creation when construction requires invariant checks.
  ```csharp
  public static Payment Create(int orderId, int customerId, int currencyId, decimal cost)
  {
      if (cost <= 0) throw new BusinessException("Payment cost must be positive");
      return new Payment { ... };
  }
  ```

- **No `ApplicationUser` navigation properties** — domain models must not reference `ApplicationUser`.
  Use `string UserId` only. `ApplicationUser` belongs exclusively to the Identity BC.
  ```csharp
  // Correct
  public string UserId { get; private set; }

  // Wrong
  public ApplicationUser User { get; set; }
  ```

- **No Law of Demeter violations** — do not chain through navigation properties to reach values.
  Pass required values as parameters or use a dedicated value object.
  ```csharp
  // Wrong — chains through CouponUsed → Coupon → Discount
  var discount = (1 - (CouponUsed?.Coupon?.Discount / 100M) ?? 1);

  // Correct — pass the resolved discount as a parameter
  public void CalculateCost(decimal discountRate) { ... }
  ```

- **Invariant checks belong in the aggregate** — never duplicate domain rule checks in handlers or services
  if they logically belong to the aggregate. Handlers only coordinate; aggregates enforce.

### EF Core compatibility note

EF Core requires a parameterless constructor for entity materialization. Add a `private` or `protected`
parameterless constructor alongside factory methods when introducing them:
```csharp
private Payment() { } // for EF Core
public static Payment Create(...) { return new Payment { ... }; }
```
