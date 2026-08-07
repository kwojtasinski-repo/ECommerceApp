---
description: "Testing guidance for unit, integration, Web integration, browser E2E, and backend E2E tests in ECommerceApp"
applyTo: "ECommerceApp.UnitTests/**, ECommerceApp.IntegrationTests/**, ECommerceApp.Web.IntegrationTests/**, ECommerceApp.Web.E2E/**, ECommerceApp.E2E.Backend/**, ECommerceApp.Shared.TestInfrastructure/**"
---

# Testing Guidelines for ECommerceApp

Purpose
- Ensure consistent, reliable tests for CI and local development.

Logging
- Test hosts and test runs use `Debug` as the default minimum log level.
- Keep Debug logs enabled when diagnosing failures; do not raise the default to `Information` or higher because it hides startup, DI, request, database, and cleanup evidence.
- A narrower per-category or per-test override is allowed only for a documented reason and must not remove the logs needed to diagnose the test.

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

Browser E2E tests (Playwright) — `ECommerceApp.Web.E2E`
- Use only when a flow needs real browser JavaScript execution and/or must observe asynchronous
  message-broker/Outbox-poller timing (a page rendering before an event is processed). See
  `create-web-e2e-test` skill for the fixture pattern and template.
- This is the one tier that deliberately keeps `MessagingOptions.UseBackgroundDispatcher = true`
  (real `BackgroundMessageDispatcher` + `OutboxPollerService`) — every other tier here runs the
  broker synchronously on purpose. Do not "fix" this by making it synchronous.
- Always host through `PlaywrightWebApplicationFactory.StartKestrelHost()` / `.ServerAddress`.
  Never use the inherited `Services`/`CreateClient()`/`Server` — they build a second, disconnected
  TestServer-backed host with its own InMemory database (see the class's own XML doc for why).
- Share the browser process via `[Collection(PlaywrightCollection.Name)]` — never launch a second
  `IBrowser`/`IPlaywright` per test class.

CI
- CI must run unit and integration tests on PRs.
- Use a separate test DB instance for integration tests; don't point tests to developer local DB.

Coverage
- Aim for high coverage in application and domain layers. No enforced % but critical paths must be covered.

Flaky tests
- Investigate and fix flaky tests; do not mark as skipped without triage.

