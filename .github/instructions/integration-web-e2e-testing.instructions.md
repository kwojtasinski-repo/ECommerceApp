---
description: "Guidance for ASP.NET Core integration, Web integration, and backend E2E tests"
applyTo: "ECommerceApp.IntegrationTests/**, ECommerceApp.Web.IntegrationTests/**, ECommerceApp.E2E.Backend/**, ECommerceApp.Shared.TestInfrastructure/**"
---

# Integration, Web, and E2E Testing Guidelines

## Scope

- These rules apply to tests that start the application host, call HTTP endpoints, use `WebApplicationFactory`, or exercise real infrastructure.
- `ECommerceApp.Shared.TestInfrastructure` is part of this scope because its factories, log sinks, database replacement, seeding, and message broker setup control test behavior.
- Keep unit-test rules in `testing.instructions.md`; do not apply unit-test mocking assumptions to these suites.

## Choose the test level deliberately

- Use service-level integration tests for DI wiring, service behavior across repositories, identity context, EF Core state, and synchronous cross-BC message handling.
- Use Web/API integration tests for routing, model binding, validation, authentication, authorization, middleware, serialization, status codes, and response bodies.
- Use backend E2E tests when the production infrastructure path matters: real SQL Server, migrations, startup configuration, container readiness, or external process boundaries.
- Do not replace an E2E dependency with an in-memory provider when the failure concerns that dependency.

## Repository conventions

- Legacy service tests extend `BaseTest<TService>` and use `CustomWebApplicationFactory<Startup>`.
- New BC service tests extend `BcBaseTest<TService>` and use `BcWebApplicationFactory`.
- API controller tests use `IClassFixture<CustomWebApplicationFactory<Startup>>` or a focused factory and an authenticated Flurl client.
- Web/API integration tests use Shouldly for HTTP assertions. Service-level integration tests use FluentAssertions unless the neighboring test class establishes another local convention.
- Backend E2E tests use the real-infrastructure factory and test fixture already provided by the project. Do not bypass migrations or container readiness in a test whose purpose is to verify them.
- Preserve existing test names. New tests use `Method_Conditions_ExpectedResult`.

## Isolation and lifecycle

- Use a unique database or collection per test class or fixture where shared state can affect results.
- Let factory and fixture cleanup run. Do not suppress `Dispose`, `EnsureDeleted`, container disposal, or log-sink cleanup to make a test pass.
- Avoid parallel execution when tests share ports, database names, Qdrant collections, filesystem paths, or singleton test doubles. Prefer explicit collection fixtures or unique names over arbitrary sleeps.
- Use cancellation tokens from the current test run for HTTP, database, and container operations.
- Treat startup, seed, migration, and authentication failures as setup failures until evidence proves the product behavior is wrong.

## Logging

- Test hosts and test runs use `Debug` as the default minimum log level.
- Keep Debug logs enabled during diagnosis; startup, DI, request pipeline, database, container, and cleanup failures often appear below `Information`.
- A narrower per-category or per-test override is allowed only for a documented reason and must not remove the logs needed to diagnose the test.

## Assertion-first, log-first diagnosis

When a test fails, do not guess from a timeout, HTTP 500, or final exception alone.

1. Identify the exact test, source assertion, expected value, actual value, status code, and response body.
2. Preserve the first failure with a focused `dotnet test` run using detailed console output and a TRX logger.
3. Read the test output and xUnit sink logs before changing the test or production code.
4. Correlate the assertion with application startup/request logs, then database, container, and external dependency logs as applicable.
5. Classify the failure as product behavior, assertion mismatch, test setup/isolation, startup/pipeline, database/infrastructure, external dependency, or timing/concurrency.
6. Run one discriminating check that can distinguish the leading causes, then make the smallest justified change.

Example on Windows PowerShell:

```powershell
$log = Join-Path $env:TEMP "integration-test.log"
dotnet test <project-or-solution> --filter "FullyQualifiedName~<test-name>" --logger "console;verbosity=detailed" --logger "trx;LogFileName=integration.trx" 2>&1 | Tee-Object -FilePath $log
```

For HTTP failures, inspect the response body and server exception. For timeouts, inspect the last completed operation, cancellation token, host lifecycle, container readiness, and dependency logs. Never increase the timeout or weaken an assertion before locating the blocked operation.

## Safe change rules

- Do not add `Skip`, `xfail`, retries, arbitrary delays, or broad status-code acceptance without a documented cause and a tracking reference.
- Do not convert a real integration/E2E test into a unit test merely to remove an environmental failure.
- Do not change the test assertion until the contract and the actual failure evidence have been compared.
- When behavior changes, add or update the narrowest test that proves the behavior and keep the original failure scenario covered.