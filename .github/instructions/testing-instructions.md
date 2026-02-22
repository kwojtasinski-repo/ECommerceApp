---
description: "Testing guidance for unit and integration tests in ECommerceApp" 
applyTo: "ECommerceApp.UnitTests/**, ECommerceApp.IntegrationTests/**"
---

# Testing Guidelines for ECommerceApp

Purpose
- Ensure consistent, reliable tests for CI and local development.

Unit tests
- Use `xUnit`, `Moq`, and `FluentAssertions`.
- Name tests using the `Method_Conditions_ExpectedResult` pattern (PascalCase, underscores as separators between the three parts):
  ```
  Method          — the method or operation under test
  Conditions      — the scenario or input state (concise, no spaces)
  ExpectedResult  — what should happen
  ```
  Examples:
  ```csharp
  public void Login_InvalidPassword_ShouldReturnInvalidCredentials()
  public void PlaceOrder_EmptyCart_ShouldThrowBusinessException()
  public void MarkAsPaid_AlreadyPaidOrder_ShouldThrowBusinessException()
  public void CalculateCost_WithActiveDiscount_ShouldApplyDiscountRate()
  ```
- **Existing tests** use the legacy `given_<context>_when_<action>_should_<result>` pattern — do NOT rename them. New tests always use `Method_Conditions_ExpectedResult`.
- Keep unit tests fast and deterministic — avoid I/O and external services.
- Use in-memory repositories from `UnitTests/Common/` for data-layer mocking.
- Use `BaseTest` for shared AutoMapper configuration.

Integration tests — two patterns

Pattern 1: Service-level integration tests
- Extend `BaseTest<TService>` (from `IntegrationTests/Common/BaseTest.cs`) — resolves `TService` from DI via `CustomWebApplicationFactory<Startup>`.
- Use `SetHttpContextUserId()` and `SetUserRole()` helpers to control current user identity.
- Use `FluentAssertions` for assertions.
- `Dispose()` calls `EnsureDeleted()` — always let it run; do not suppress.

Pattern 2: API controller integration tests
- Use `IClassFixture<CustomWebApplicationFactory<Startup>>` directly — NOT `BaseTest<T>`.
- Use `_factory.GetAuthenticatedClient()` to get an authenticated **Flurl** HTTP client.
- Use **Shouldly** for assertions (`ShouldBe`, `ShouldNotBeNull`, `ShouldBeGreaterThan`, etc.).
- Tests hit real HTTP endpoints and verify full request/response pipeline.
- Test naming follows the same `Method_Conditions_ExpectedResult` pattern.

CI
- CI must run unit and integration tests on PRs.
- Use a separate test DB instance for integration tests; don't point tests to developer local DB.

Coverage
- Aim for high coverage in application and domain layers. No enforced % but critical paths must be covered.

Flaky tests
- Investigate and fix flaky tests; do not mark as skipped without triage.

