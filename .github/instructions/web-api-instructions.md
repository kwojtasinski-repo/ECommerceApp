---
description: "Guidelines for Web API controllers and DTOs in ECommerceApp.API"
applyTo: "ECommerceApp.API/**/*.cs, ECommerceApp.API/**/**/*.cs"
---

# Web API Guidelines (ECommerceApp.API)

Purpose
- Rules for creating and modifying HTTP APIs in `ECommerceApp.API`.

Key rules
- Controllers must be thin: validate input (ModelState), map DTOs to service calls, return proper IActionResult / typed ActionResult<T>.
- Use DTOs (suffix `Dto`) for all API boundaries. Do not return domain entities.
- Follow REST semantics: use correct HTTP verbs. Return `Ok()` with id for creates, `NotFound()` for missing resources, `Ok()` for updates — match existing controller conventions before changing return types.
- Use `[ApiController]` and rely on automatic model validation; avoid manual ModelState checks.
- Prefer `CancellationToken` on async endpoints — do not add unless the service method accepts it.
- All API methods must be async and call application services async methods.

Security & auth
- Enforce `[Authorize]` on protected endpoints. Use role constants from `UserPermissions.Roles` or `BaseController` role constants.
- Validate authorization inside services only when required for business logic; prefer attributes for coarse-grained control.

Error handling and responses
- Exception handling pipeline: see [`dotnet-instructions.md §4`](../instructions/dotnet-instructions.md).
- For expected business errors, return appropriate status codes using `BadRequest`, `NotFound`, `Conflict`, etc., mapping from `BusinessException` when needed.
- Use `ProblemDetails` or typed error response models (`ExceptionResponse`) for API error contracts.

Versioning & contracts
- Keep API changes backward compatible. For breaking changes, create a new route version (e.g., `api/v2/...`) and record ADR.
- Update DTOs and update integration tests when altering API contracts.

Testing
- API integration tests use `IClassFixture<CustomWebApplicationFactory<Startup>>` — NOT `BaseTest<T>` (which is for service-level integration tests).
- API integration tests use **Flurl** (`client.Request(...)`) for HTTP calls and **Shouldly** (`ShouldBe`, `ShouldNotBeNull`) for assertions.
- Use `_factory.GetAuthenticatedClient()` to get an authenticated HTTP client in tests.
- Test naming follows `Method_Conditions_ExpectedResult` — see [`testing-instructions.md`](../instructions/testing-instructions.md).

Documentation
- Keep OpenAPI (Swagger) updated for new endpoints and models. Document expected status codes and error responses.
