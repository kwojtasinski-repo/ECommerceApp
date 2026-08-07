---
name: diagnose-integration-e2e-tests
description: >
  Diagnose failed .NET integration tests and E2E backend tests from the concrete failing
  assertion and captured logs. Use when dotnet test, WebApplicationFactory, API integration,
  E2E backend, HTTP, database, Docker, or CI tests fail. Never guess the root cause: identify
  the exact test and assertion first, then inspect test-runner, application, database, and
  container logs before proposing a fix.
argument-hint: "<test command, failure output, log path, or test name>"
---

# Diagnose integration and E2E backend tests

This is a diagnostic workflow, not a test-writing or test-stabilisation shortcut. The goal is
to explain one observed failure with evidence and to separate assertion defects from application,
infrastructure, environment, and test-isolation failures.

## When to use

- A test in `ECommerceApp.IntegrationTests`, `ECommerceApp.Web.IntegrationTests`,
  `ECommerceApp.Web.E2E`, or `ECommerceApp.E2E.Backend` fails.
- `dotnet test` reports a failed test, timeout, HTTP error, database error, or host startup error.
- A CI run reports only a generic failure and the underlying logs must be reconstructed.
- A test passes alone but fails in a suite or in a repeated run.

## When not to use

- Unit-test-only failures with no application host or external dependency: use the normal unit-test
  workflow.
- RAG MCP or context-mode startup failures: use `diagnose-rag` or `ctx-doctor-playbook`.
- A request to add a new test without an observed failure: use `create-integration-test` or the
  relevant implementation workflow.

## Non-negotiable rules

1. **Never guess the root cause from the test name, exception type, or exit code alone.**
2. **Find the exact failing test and the exact assertion before reading broadly.** Record the
   expected value, actual value, assertion method, source file, and line if available.
3. **Inspect logs before proposing code changes.** At minimum inspect the test-runner output and,
   when applicable, application-host, HTTP, database, Docker, and browser/client logs.
4. **Do not replace evidence with a rerun.** A rerun is a discriminating check, not proof that the
   first failure did not matter.
5. **Do not weaken, delete, skip, or xfail a test** to make the run green. Read
   `.github/context/test-stabilization-policy.md` before any quarantine discussion.
6. **Preserve the first failure.** Save or reference the original command, exit code, timestamp,
   test name, assertion, and relevant log window before retrying.
7. **One hypothesis at a time.** Every proposed cause must name the evidence that supports it and
   the cheapest check that could falsify it.

## Evidence order

Use this order so infrastructure noise does not hide a product assertion:

1. **Test identity** — project, class, method, trait/category, and exact failing case.
2. **Assertion** — assertion library and expression; expected vs actual; response status/body or
   persisted state involved.
3. **Test-runner evidence** — `dotnet test` console output, stack trace, duration, retries, and
   result summary. Prefer TRX when console output is truncated.
4. **Application-host evidence** — ASP.NET startup, middleware, request, domain, EF Core, and
   exception logs covering the same timestamp/request/correlation id.
5. **Dependency evidence** — SQL Server, Docker container, message broker, external API, or test
   database logs, only for dependencies used by the failing path.
6. **Reproduction check** — rerun the smallest targeted test with diagnostic logging enabled, then
   rerun the relevant broader scope only after the local cause is understood.

## Workflow

### 1. Capture the failure exactly

Start from the supplied output or run the narrowest command that reproduces it. On Windows, use
PowerShell and keep both the output and exit code:

```powershell
$log = "artifacts/test-diagnostics/$(Get-Date -Format yyyyMMdd-HHmmss)-integration.log"
New-Item (Split-Path $log) -ItemType Directory -Force | Out-Null
dotnet test <project-or-solution> --filter "FullyQualifiedName~<test-name>" `
  --logger "console;verbosity=detailed" --logger "trx;LogFileName=integration.trx" `
  2>&1 | Tee-Object -FilePath $log
$exitCode = $LASTEXITCODE
```

If the user already supplied a log, do not rerun before extracting the first failure from it.

### 2. Identify the assertion

Produce a small evidence record:

```text
TEST: <fully qualified test name>
ASSERTION: <framework + expression / source location>
EXPECTED: <value or condition>
ACTUAL: <value or condition>
REQUEST: <method + route + relevant input, if HTTP>
DEPENDENCIES: <DB / Docker / broker / external API>
FIRST FAILURE: <timestamp and first meaningful exception>
```

For Shouldly/FluentAssertions/xUnit failures, distinguish assertion message, inner exception, and
stack frame. For HTTP tests, capture status code, headers relevant to the failure, and response
body. For database assertions, capture the queried state and transaction/cleanup context.

### 3. Correlate the log window

Search around the first failure timestamp, test name, request route, correlation id, or exception
message. Do not read only the last lines of a container log: startup failures often precede the test
failure by several seconds.

Useful host-side sources include:

```powershell
docker compose logs --since 10m <service>
docker logs --since 10m <container>
Get-Content <app-log> | Select-String -Pattern '<test-name>|<route>|<correlation-id>|Exception|fail|error'
```

For CI, inspect downloaded artifacts and TRX XML before relying on the CI summary. Redact secrets,
tokens, cookies, connection strings, and personal data in any report.

### 4. Classify the failure

Assign exactly one primary classification, with evidence:

| Classification | Typical evidence |
|---|---|
| Assertion/product behavior | Host and dependencies are healthy; actual state contradicts the contract |
| Test setup/data isolation | Fixture, seed, cleanup, user identity, clock, or ordering differs from expectation |
| Application startup/request pipeline | Host failed to start, middleware rejected request, DI/configuration error |
| Database/infrastructure | Connection, migration, lock, timeout, transaction, or container health error |
| External dependency | Message broker, API, filesystem, or network dependency failed |
| Timing/concurrency | Race, eventual consistency, background dispatcher, deadlock, or timeout with evidence |

A timeout is not a classification by itself. Find what was waiting and why.

### 5. Run one discriminating check

Choose the cheapest check that can falsify the classification, for example:

- rerun the single test with `--filter`, not the whole solution;
- verify the dependency/container health and exact connection target;
- run the same test sequentially or with one fixture instance;
- query the test database for the expected persisted row/state;
- inspect the response body and server log for the same request;
- compare the failing test's setup with a passing neighbouring test.

Report the check and its result. If it falsifies the hypothesis, move one level closer to the
component that directly controls the behavior; do not patch unrelated code.

### 6. Report before fixing

Use this output shape:

```markdown
# Integration/E2E Test Diagnosis

## Verdict
- **Test**: ...
- **Primary classification**: ...
- **Confidence**: high / medium / low

## Assertion evidence
- **Expected**: ...
- **Actual**: ...
- **Assertion**: ...
- **Source**: ...

## Log evidence
- **Test runner**: ...
- **Application/dependency logs**: ...
- **Correlation**: ...

## Root cause
[Only what the evidence supports. Separate confirmed cause from open hypothesis.]

## Discriminating check
- **Check**: ...
- **Result**: ...

## Smallest fix
[File/symbol and why it owns the behavior.]

## Validation
[Exact targeted test first, then any broader command required.]

## Residual risk
[Flakiness, cleanup, parallelism, environment, or missing log coverage.]
```

Only after this diagnosis should implementation begin. After a fix, rerun the same targeted test,
then the affected integration/E2E slice, and retain the new output for comparison.

## Common failure traps

- **Only reading the assertion text**: the assertion may be correct while the host failed to seed,
  authenticate, migrate, or return the intended response.
- **Only reading application logs**: a test setup or cleanup failure can produce a misleading server
  symptom; always pair host logs with the test stack trace.
- **Treating HTTP 500 as the cause**: inspect the response body and server exception/correlation id.
- **Treating a timeout as flaky**: locate the blocked operation, then inspect DB locks, background
  queues, container health, and cancellation behavior.
- **Rerunning the whole suite first**: this destroys the smallest reproducible signal and can change
  ordering-dependent behavior.
- **Adding a skip without a tracking reference**: forbidden by the test stabilization policy.
- **Using production data or secrets in diagnostics**: use the test database and redact all output.

## Related project rules

- `.github/instructions/testing.instructions.md`
- `.github/context/test-stabilization-policy.md`
- `.github/context/known-issues.md` for confirmed bugs before bug fixes
- `create-integration-test` for scaffolding
- `test-strategy-reviewer` for reviewing test level and mocking choices
