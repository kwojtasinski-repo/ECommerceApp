# Test Stabilization Policy

Applies to **all test suites** in this workspace:
`ECommerceApp.UnitTests`, `ECommerceApp.IntegrationTests`, `ECommerceApp.Web.IntegrationTests`,
`tools/rag` (pytest), `tools/rag-dotnet` (xUnit).

---

## 1. No Silent Skips

**Rule**: A test must never be silenced without a traceable reason.

| Stack | Forbidden | Required alternative |
|---|---|---|
| Python (pytest) | `pytest.skip()` with no reason | `pytest.skip(reason="KI-NNN: ...")` |
| Python (pytest) | `@pytest.mark.skip` with no reason | `@pytest.mark.skip(reason="KI-NNN: ...")` |
| Python (pytest) | bare `xfail` | `@pytest.mark.xfail(strict=False, reason="KI-NNN: ...")` |
| .NET (xUnit) | `[Fact(Skip="")]` (empty reason) | `[Fact(Skip="KI-NNN: <description>")]` |
| .NET (xUnit) | `Assert.True(true)` placeholder | Remove the test or implement it |
| Both | `catch (Exception) { /* ignore */ }` in test helpers | Fix the helper; never swallow |

Every skip/xfail MUST reference one of:
- A `KI-NNN` entry in [known-issues.md](known-issues.md)
- A GitHub issue number
- An ADR number (for architectural deferrals)

---

## 2. Test Categories

All tests must be tagged with exactly one category. Category determines where the test runs.

| Category | Meaning | Python marker | .NET trait |
|---|---|---|---|
| `unit` | Pure logic — no I/O, no network, no model | `@pytest.mark.unit` | `[Trait("Category","unit")]` |
| `integration` | Needs a running dependency (DB, Qdrant) but no full stack | `@pytest.mark.integration` | `[Trait("Category","integration")]` |
| `e2e` | Full stack — real DB/Qdrant + real model + real server | `@pytest.mark.e2e` | `[Trait("Category","e2e")]` |
| `infra` | Docker lifecycle, port checks, container startup | `@pytest.mark.infra` | `[Trait("Category","infra")]` |

**CI gate rules:**
- `unit` — always run (no external deps).
- `integration` — run only when the required service is confirmed up.
  - ECommerceApp: SQL Server from `appsettings.test.json`.
  - RAG: `docker compose up -d qdrant` must be done first.
- `e2e` — never run in a standard CI unit-test job. Require `ENABLE_E2E=true` env var
  OR an explicit pytest `-m e2e` invocation.
- `infra` — Docker daemon required; skip entirely in environments without Docker.

---

## 3. Flaky Test Protocol

A flaky test is one that passes or fails non-deterministically across runs with no code changes.

**Step 1 — Identify**: First flaky failure observed in CI or local run.

**Step 2 — Quarantine immediately** (same PR if possible):

```python
# Python
@pytest.mark.xfail(strict=False, reason="KI-NNN: intermittent on shared Qdrant instance")
def test_something():
    ...
```

```csharp
// .NET — add a Skip reason until fixed
[Fact(Skip = "KI-NNN: intermittent on shared SQL Server — parallel test isolation needed")]
public async Task Something_Works() { ... }
```

**Step 3 — Track**: Open / reference a `KI-NNN` entry in [known-issues.md](known-issues.md) with:
- Failure rate (e.g. "1 in 5 runs")
- Suspected cause
- Owner

**Step 4 — Fix deadline**: Fix or re-classify within **2 sprints**.
- If fixed → remove `xfail`/`Skip`, close KI entry.
- If not fixable in 2 sprints → move test to `tests/infra/` (Python) or `Infrastructure/` subfolder (.NET)
  and exclude from default CI matrix permanently. Document decision in the KI entry.

---

## 4. Environment-Dependent Tests

Some tests require live infrastructure (Qdrant, SQL Server, Docker, model files).
These **must** be isolated so they never block the standard unit-test run.

### Python (pytest)
```ini
# pytest.ini / pyproject.toml
[tool.pytest.ini_options]
addopts = "-m 'not e2e and not infra'"   # default: skip e2e+infra
```
Run E2E explicitly:
```bash
pytest -m e2e                 # all e2e
ENABLE_E2E=true pytest        # same effect via env gate
```

### .NET (xUnit)
Use `[Trait("Category","integration")]` and filter in the CI pipeline:
```bash
dotnet test --filter "Category!=e2e&Category!=infra"   # standard job
dotnet test --filter "Category=integration"             # integration job
```

---

## 5. Test Data Hygiene

- **Isolation**: each test must clean up its own data. Never depend on run order.
- **Shared state**: no `static` or `public static` mutable fields shared across tests.
  Use fixtures / `IClassFixture<T>` / `TestDatabase` helpers.
- **Database state**: integration tests must reset DB to a known state before each run.
  - ECommerceApp: `CustomWebApplicationFactory` resets via `TestDatabaseInitializer`.
  - RAG: use a dedicated Qdrant collection per test run (e.g. `test_{uuid}`), delete after.
- **Clock**: never use `DateTime.Now` / `datetime.now()` in assertions. Inject `IClock` or use fixed timestamps.

---

## 6. Pre-Commit Checklist

Before opening a PR that adds or modifies tests:

- [ ] Every new `skip` / `xfail` / `[Fact(Skip=...)]` has a tracking reference (`KI-NNN` or issue #).
- [ ] New E2E tests are marked `@pytest.mark.e2e` / `[Trait("Category","e2e")]`.
- [ ] New tests that require Qdrant/SQL Server are marked `integration` or `e2e`, not `unit`.
- [ ] No `Assert.True(true)`, `Assert.Pass()`, or empty test bodies.
- [ ] No test helper that swallows exceptions silently.
- [ ] Test isolation confirmed: test passes independently of run order.

---

## 7. Known Flaky Tests Register

Update this table whenever a test is quarantined or fixed.

| Test | Stack | Reason | KI ref | Status |
|---|---|---|---|---|
| `test_e2e.py::test_*` | Python pytest | Requires live Qdrant + model | KI-pending | Quarantined under `@pytest.mark.e2e` |
| `test_full_pipeline.py` | Python pytest | Requires Docker daemon | KI-pending | Must run with `ENABLE_E2E=true` |
| *(add new entries here)* | | | | |

---

*Last updated: see git log.*
