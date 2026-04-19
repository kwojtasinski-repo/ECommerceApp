# ADR-0001: ECommerceApp � Project Overview and Technology Stack

## Status
Accepted

## Date
2026-02-21

## Context

This is the foundational ADR for ECommerceApp. It documents the full project scope, architecture,
technology choices, and conventions so that every contributor and automation agent has a single
authoritative reference before making any changes to the codebase.

ECommerceApp started as a learning/portfolio project to demonstrate clean/onion architecture
in a real-world e-commerce domain using the .NET ecosystem. The scope grew to include:
- A server-side rendered MVC web frontend.
- A REST API with JWT authentication for programmatic access.
- A full test suite covering unit and integration scenarios.
- An Identity area for user authentication and management.

The key forces that shaped the initial technology choices were:
- Familiarity with the .NET ecosystem and ASP.NET Core.
- Desire to practice clean/onion architecture with real domain complexity.
- Need for both a UI (for end users) and an API (for programmatic consumers).
- MSSQL as the target database (common in enterprise .NET projects).

## Decision

We document the full stack, project structure, domain, and conventions as the single
source of truth for this repository. All future ADRs must reference this document
and extend or supersede specific decisions recorded here.

### Solution structure

The solution `ECommerceApp.sln` contains the following projects:

- `ECommerceApp.Domain` � domain models (`BaseEntity` subclasses, 22 domain model classes) and repository interfaces (`IGenericRepository<T>` and domain-specific interfaces). No dependencies on other layers.
- `ECommerceApp.Application` � application services, handlers, DTOs, ViewModels, validators (FluentValidation), AutoMapper profiles, middleware, exception types. Depends only on `Domain`.
- `ECommerceApp.Infrastructure` � EF Core `Context`, `GenericRepository<T>`, concrete repositories, auth (JWT, Identity internals), migrations (12 migration files as of 2026-02-21), seed data. Depends on `Application` and `Domain`.
- `ECommerceApp.Web` � ASP.NET Core MVC application. 18 MVC controllers + Views, Identity Razor Pages area under `Areas/Identity/Pages/`. Thin UI layer; depends on `Application`.
- `ECommerceApp.API` � ASP.NET Core Web API. 15 REST controllers with JWT authentication. Thin API layer; depends on `Application`.
- `ECommerceApp.UnitTests` � 21 unit test files using xUnit, Moq, FluentAssertions.
- `ECommerceApp.IntegrationTests` � 36 integration test files using xUnit, Flurl, Shouldly, `CustomWebApplicationFactory<Startup>`.

### Business domain

ECommerceApp covers the following bounded domain areas:

- **Catalog**: products (`Item`) with images (`Image`), brands (`Brand`), tags (`Tag`), types (`Type`).
- **Orders**: customer orders (`Order`, `OrderItem`) with cart and checkout flows.
- **Payments**: payment processing and state tracking (`Payment`, `PaymentState`).
- **Refunds**: refund requests and lifecycle (`Refund`).
- **Coupons**: discount coupons with types and usage tracking (`Coupon`, `CouponType`, `CouponUsed`).
- **Customers**: profiles with addresses and contact details (`Customer`, `Address`, `ContactDetail`, `ContactDetailType`).
- **Currencies**: multi-currency support with external rate integration via NBP (National Bank of Poland) API (`Currency`, `CurrencyRate`).
- **Identity & User Management**: ASP.NET Core Identity for authentication, role-based access (`Administrator`, `Manager`, `Service`, `User`), Google OAuth, admin user management UI.

### Backend technology stack

- ASP.NET Core MVC (UI) and Web API (REST).
- Entity Framework Core with MSSQL.
- LINQ for all query composition.
- FluentValidation for input validation (validators live in `Application` layer).
- AutoMapper for mapping between domain entities and ViewModels/DTOs (`IMapFrom<T>` interface).
- ASP.NET Core Identity (cookie-based auth for Web, JWT for API).
- Google OAuth for Web login.
- `JwtManager` in `Infrastructure/Auth/` for JWT token generation and validation.
- `UserContext` (`IUserContext`) for current user identity in services.

### Key architectural patterns

- `AbstractService<TVm, URepo, EEntity>` � base class for all CRUD-oriented application services.
- **Handler pattern** � `CouponHandler`, `PaymentHandler`, `ItemHandler` for complex cross-aggregate operations.
- `GenericRepository<T>` � base repository in `Infrastructure` implementing `IGenericRepository<T>`.
- `ExceptionMiddleware` + `BusinessException` + `IErrorMapToResponse` � unified exception handling pipeline.
- `IFileStore` / `IFileWrapper` � file/image storage abstractions (no raw `System.IO` in services).
- `CurrencyRateService` + `NBPResponseUtils` � external NBP API integration for currency rates.
- `BaseController` (Web + API) � shared helpers: `GetUserId()`, `GetUserRole()`, `MapExceptionAsRouteValues()`, role constants.
- `ModelStateFilter` � global MVC model validation filter (returns `400 BadRequest` automatically).

### Frontend technology stack (`ECommerceApp.Web`)

Managed via LibMan (`libman.json`):
- Bootstrap � layout and responsive UI.
- Bootstrap Select � enhanced dropdowns.
- Font Awesome � icons (solid and brands).
- jQuery � DOM manipulation and AJAX.
- jQuery Validation + Unobtrusive � client-side form validation.
- Globalize + CLDR.js + jquery-validation-globalize � locale-aware number/date/currency formatting.
- require.js � AMD module loader for custom JS.

Custom JS modules under `wwwroot/js/`:
- `ajaxRequest.js` � AJAX abstraction (used for cart item count on every page load).
- `modalService.js`, `dialogTemplate.js`, `buttonTemplate.js` � dynamic UI components.
- `forms.js`, `validations.js`, `errors.js` � form helpers and error display.
- `config.js`, `site.js` � require.js config and global initialization.

Frontend notes:
- UI navigation labels are partially in Polish (`Koszyk`, `Moje zam�wienia`, `Przedmioty`). Do not change without explicit request.
- Role-based navigation is controlled by `UserPermissions.Roles` in `_Layout.cshtml`.
- Cart item count is loaded dynamically via AJAX using `ajaxRequest.js` on every page.

### Testing conventions

- Unit tests: xUnit + Moq + FluentAssertions. Extend `BaseTest` (provides `IMapper`). Use in-memory repositories from `UnitTests/Common/`.
- Integration tests � two patterns:
  - Service-level: extend `BaseTest<TService>` + FluentAssertions. DB cleaned via `EnsureDeleted()` in `Dispose()`.
  - API-level: `IClassFixture<CustomWebApplicationFactory<Startup>>` + Flurl HTTP client + Shouldly assertions.
- Test naming: `given_<context>_when_<action>_should_<expected_result>`.

### DI registration

- Application services registered in `ECommerceApp.Application/DependencyInjection.cs`.
- Infrastructure services registered in `ECommerceApp.Infrastructure/DependencyInjection.cs`.
- No services registered directly in `Startup.cs` / `Program.cs`.

### Database

- MSSQL with EF Core Code First.
- 12 migrations under `ECommerceApp.Infrastructure/Migrations/` as of 2026-02-21.
- DB configurations per entity under `Infrastructure/Database/Configurations/`.
- Seed data in `Infrastructure/Database/SeedData/Seed.cs` and `DatabaseInitializer`.
- Migrations require review and approval � see `migration-policy.md`.

## Consequences

### Positive
- Single authoritative reference for all future contributors and agents.
- Onion architecture enforces clear separation: no business logic in controllers, no EF Core in services.
- Full test coverage at unit and integration levels makes refactoring safe.
- Handler pattern isolates complex cross-aggregate operations and keeps services clean.

### Negative
- `AbstractService` base class introduces coupling between all services and the generic CRUD contract.
- Two separate auth systems (Identity for Web, JWT for API) require maintaining two auth configurations.
- Polish UI labels require team communication before any UI text changes.

### Risks & mitigations
- Risk: agents may create services without `AbstractService` � mitigated by explicit rule in `dotnet-instructions.md` and `copilot-instructions.md`.
- Risk: frontend library drift if LibMan is bypassed � mitigated by explicit rule to always check `libman.json`.
- Risk: DB migrations applied without review � mitigated by `migration-policy.md`.

## Alternatives considered

- **Option A** � Blazor for UI � rejected because the project targets ASP.NET Core MVC with standard server-side rendering and jQuery.
- **Option B** � Dapper instead of EF Core � rejected because EF Core Code First was chosen to leverage migrations, tracking, and the repository abstraction.
- **Option C** � Microservices � rejected because the project is a single-domain monolith for learning and portfolio purposes.

## References

- Related ADRs: none yet � this is the foundational ADR.
- Instruction files:
  - `.github/instructions/dotnet-instructions.md`
  - `.github/instructions/web-api-instructions.md`
  - `.github/instructions/razorpages-instructions.md`
  - `.github/instructions/frontend-instructions.md`
  - `.github/instructions/efcore-instructions.md`
  - `.github/instructions/migration-policy.md`
  - `.github/instructions/testing-instructions.md`
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
