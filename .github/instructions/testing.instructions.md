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

### Named mock setup helpers
- Prefer named `Setup<BusinessName>(...)` methods over inline `mock.Setup(...)` calls when a mock setup represents a meaningful business scenario or is repeated in a test class. Name the helper for the business situation, not the technical interface method, for example `SetupActiveReservationsForProduct(...)` rather than `SetupGetActiveByProductIdAsync(...)`.
- Keep helpers local to the test class first. Promote a helper to a shared static or extension-method class within the same test project only after the same setup is genuinely used by at least two test classes. Promote it to `ECommerceApp.Shared.TestInfrastructure` only after proven reuse across test projects. Never promote speculatively.
- For shared helpers, use a Moq extension method on `Mock<TInterface>` and return the mock to keep setup calls chainable:
  ```csharp
  public static Mock<ISoftReservationRepository> SetupActiveReservationsForProduct(
    this Mock<ISoftReservationRepository> mock,
    int productId,
    params SoftReservation[] reservations)
  {
    mock.Setup(r => r.GetActiveByProductIdAsync(productId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(reservations);
    return mock;
  }
  ```
- Keep the test body focused on one observable behavior. Do not use `if`, conditional
  expressions, loops, or mutable state to make one test cover multiple repository states
  or outcomes. Split those states into separate tests with explicit setup; a branch in the
  test usually means the scenario is testing too much or is coupled to implementation order.
- If a state transition must be modeled (for example, a repository sees a step only after
  `AddStepAsync`), hide that mechanics in a named setup helper such as
  `SetupStepsAvailableAfterStepAdded(...)`. The test should state the scenario, while the
  helper owns the mock callback and state tracking. Helpers may contain branching required
  to model that state transition; the test itself should not.
- This is a living convention applied to new tests and opportunistic retrofits when a file is already being changed. The two-file pilot in this change is not a completed repo-wide sweep; most existing test files may continue to use inline `mock.Setup(...)` until they are touched for another reason.

### Scenario boundaries across test levels
- The one-observable-behavior rule applies to unit tests, service/API integration tests, backend E2E tests, and browser E2E tests.
- Do not split a test merely because it has several technical steps. Multiple mock calls, HTTP requests, page interactions, or state transitions may remain in one test when they are required parts of one business scenario and the final assertion describes that scenario.
- Split the test when it combines independent outcomes, unrelated failure branches, or separate business behaviors. A test with several requests is not automatically too broad; a test that verifies several unrelated responses is.
- Integration and backend E2E tests may use named helpers for repeated mock setup, request construction, authentication, database state, and response assertions. Keep the helper named for the business scenario rather than the technical endpoint or method.
- Browser E2E tests should keep workflow mechanics in existing Page Objects or scenario components. Review those abstractions before adding another helper, and do not duplicate browser actions in individual tests.
- For all integration and E2E levels, maintain a separate backlog of test-data candidates: duplicated builders, unclear defaults, excessive fixture setup, cross-test state, and data that hides the business scenario. Improve data builders only when that makes the scenario clearer or isolation more reliable.

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
- Share the browser process via the assembly fixture (`[assembly: AssemblyFixture(typeof(
  PlaywrightBrowserFixture))]`, declared in `AssemblyFixtureConfiguration.cs`); take
  `PlaywrightBrowserFixture` as a constructor argument. Never launch a second `IBrowser`/`IPlaywright`
  per test class, and do not reintroduce a shared `[Collection]` for it — a collection is xunit's unit
  of parallelism, so that would serialize the whole suite.
- This project runs test collections in parallel (`xunit.runner.json`, capped at 4 threads). One test
  class = one collection, so put slow flows in their own class. Each test constructs its **own**
  `PlaywrightWebApplicationFactory` (own port, own IAM + bounded-context InMemory databases) and its
  own `BrowserContext`; never share a factory across tests via `IClassFixture`/`ICollectionFixture`.

CI
- CI must run unit and integration tests on PRs.
- Use a separate test DB instance for integration tests; don't point tests to developer local DB.

Coverage
- Aim for high coverage in application and domain layers. No enforced % but critical paths must be covered.

Flaky tests
- Investigate and fix flaky tests; do not mark as skipped without triage.

