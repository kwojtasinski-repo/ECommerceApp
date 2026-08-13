# Web E2E — parallelisation, `AddToCart` race, and surrounding setup (2026-08-13)

> Record of a working session on `ECommerceApp.Web.E2E`, started from a ~1-in-3 flake and a
> suspicion that parallel execution was the cause. It was not. Companion:
> [`test-infrastructure-iam-seeding-duplication-2026-08-13.md`](./test-infrastructure-iam-seeding-duplication-2026-08-13.md).

---

## 1. TL;DR

| Problem | Cause | Outcome |
|---|---|---|
| `net::ERR_ABORTED`, ~1 run in 3 | POM waited on the wrong signal after add-to-cart | Fixed, 10/10 clean |
| 30 s hang in `CartPage.NavigateAsync` | `WaitUntil=DOMContentLoaded` + `WaitForURLAsync` (waits for `Load`) | Fixed |
| Suite ran sequentially despite the collection rework | `parallelizeTestCollections: false`, in an untracked file | Enabled, 23 s → ~10 s |
| Default IAM users absent from the running host | Sweep re-pointed `IamDbContext` after the base had seeded | Fixed via the base's own hook |
| `dotnet restore/build ECommerceApp.sln` failed | 9 orphan `NestedProjects` entries | Fixed, both exit 0 |
| `result.PaymentConfirmed.ShouldBeTrue()` was a tautology | Result record was built with a literal `true` | Now derived from the admin's page |

---

## 2. The flake was never about parallelism

The starting hypothesis was cross-test interference from parallel execution. Diagnostic logging of
host start/dispose timestamps disproved it — every host was strictly disjoint:

```
9d7dfa09  start 22:22:03.53   dispose 22:22:05.50
a9be0abf  start 22:22:05.54   dispose 22:22:13.26
f11a9042  start 22:22:13.29   dispose 22:22:17.72
```

`xunit.runner.json` had `parallelizeTestCollections: false`, which overrode the collection rework
entirely. The suite was single-threaded the whole time.

The real cause was in `Areas/Presale/Views/Storefront/Details.cshtml`:

```js
const response = await fetch(form.action, { method: 'POST', ... });
if (response.ok) {
    window.location.href = response.url;   // ← navigation happens here, not at the POST
}
```

`AddToCartAsync` waited on the POST response and on `WaitForURLAsync("**/offers/{id}")`. The POST
resolves long before the assignment runs, and the URL glob was already satisfied — `returnUrl` is the
product page the test is standing on. So the POM returned early, the caller issued its next
`GotoAsync`, and Chromium aborted it in favour of the late client-side navigation.

This explains both observed failure URLs (`/offers?e2eRefresh=…` after the first add-to-cart,
`/Presale/Checkout/Cart` after the second) and why the server logged HTTP 200 for a request the
browser reported as aborted.

**Fix:** wait for the document itself to be replaced. A committed same-origin navigation gives the
page a fresh `window`, so a sentinel set before the click disappearing is the one reliable signal.

---

## 3. Parallelisation

The blocker was structural, not configuration. A collection is xunit's unit of parallelism, so
sharing the browser through `ICollectionFixture` forces every test that needs it into one collection.
xunit v3's `AssemblyFixtureAttribute` shares the same single Chromium process without that:

```csharp
[assembly: AssemblyFixture(typeof(PlaywrightBrowserFixture))]
```

Alongside it:

- `PlaywrightFixtureSmokeTests` (5 tests, one class) split into four classes so each flow is its own
  collection; the two slowest lifecycle tests are deliberately separated.
- `maxParallelThreads: 4` — each test boots a full Kestrel host plus a browser context.
- Seeding extracted to `Infrastructure/E2ESeed.cs`.

Resource model after the change, per run: **1** Chromium process, **7** browser contexts, **6**
Kestrel hosts. Verified overlapping: four hosts start in the same millisecond on four threads, and the
pool refills as slots free.

**23 s → ~10 s, 6/6 across 10 consecutive runs.**

### Isolation

Each test constructs its own factory. Measured, not assumed — resolving `IamDbContext` from two live
hosts and reading the InMemory store name:

```
A iam=BcTestDb_IamDbContext_82ad…   B iam=BcTestDb_IamDbContext_75b2…   B sees A's user = False
```

Bounded-context databases get `BcTestDb_{Type}_{Guid}` per host from `BcDbContextTestSetup`. The
monolithic `Context` is registered nowhere, so no test can reach a real SQL Server.

### A measurement artefact worth knowing about

One run reported three failures with a duration of "5 h 48 m". The machine had suspended mid-run:
the previous run was at `23:02Z`, the next clock read was `04:53Z`, and the difference matches the
reported duration. Playwright's 30 s timeouts had long expired on wake. Not a parallelism defect —
but in a raw log it looks exactly like one.

---

## 4. Setup fixes made along the way

**Test config was not in the repo.** `.gitignore`'s blanket `*.json` swallowed `xunit.runner.json` and
`appsettings.test.json` for both `ECommerceApp.Web.E2E` and `ECommerceApp.E2E.Backend`. Two
consequences: the parallelism decision lived only on one developer's disk, and a fresh clone could not
start either project at all (`appsettings.test.json` is loaded with `optional: false`). This matters
most for `E2E.Backend`, which deliberately runs sequentially against one shared SQL Server container —
without its `xunit.runner.json` a clone silently defaults to parallel collections.

**IAM seeding landed in an orphan store.** `PlaywrightWebApplicationFactory` ran its DbContext sweep
from a second `ConfigureServices` callback, i.e. after the base class had already seeded. Moving it
into the base's `OverrideServicesImplementation` hook fixed it; `test@test`, `test2@test2` and
`admin@localhost` are now present in the running host. Details and the remaining copy of this pattern:
see the companion report.

**`ECommerceApp.sln` did not load.** Nine `NestedProjects` entries referenced GUIDs with no
`Project(...)` declaration — leftovers from removed RAG solution-folder items. MSBuild reports only
the first, so this needed sweeping the whole section rather than fixing one error at a time.
`dotnet restore` and `dotnet build` on the solution now both exit 0. CI (`dotnet-ci.yml`) operates on
the solution, so it could not have passed in this state; CI is currently disabled by choice.

---

## 5. Making the payment assertion real

`GuestOrderLifecycleScenario` returned `new OrderLifecycleResult(orderId, true, …)` — a literal. Both
lifecycle tests asserted `result.PaymentConfirmed.ShouldBeTrue()`, which tested nothing.

`Order.cs:98` sets `OrderStatus.PaymentConfirmed`, and `Areas/Sales/Views/Orders/Fulfillment.cshtml`
renders it. So the value is observable through the admin persona already in the scenario:

```csharp
Task<string> GetOrderStatusAsync();   // IOrderFulfillmentPage
// dt:text-is('Status') + dd  — matched by label, not position
```

The assertion now proves the customer's payment reached the read model the back office works from —
which also exercises the Outbox path end to end.

A point-in-time read was chosen over a retry loop deliberately. `OrderSummaryPage.OpenPaymentAsync`
already reload-polls up to ten times, which suggested this path might be eventually consistent, so the
simple version was measured first rather than pre-emptively wrapped in another poll.

**Measured: stable, 5/5 runs.** By the time the admin opens the fulfillment page the status has
already propagated, so no wait is needed here. The assertion is now load-bearing — any other rendered
status yields `false` and fails the test, so a pass proves the string matched exactly.

---

## 6. Verification

| Suite | Tests | Result |
|---|---|---|
| `ECommerceApp.UnitTests` | 1072 | pass |
| `ECommerceApp.IntegrationTests` | 247 | pass |
| `ECommerceApp.Web.IntegrationTests` | 20 | pass |
| `ECommerceApp.Web.E2E` | 6 | pass |
| `ECommerceApp.E2E.Backend` | 19 | pass |

`ECommerceApp.Shared.TestInfrastructure` carries only a corrected comment; no behavioural change
outside `ECommerceApp.Web.E2E`.

---

## 7. Still open

- **`.github/plans/03-phase-web-e2e-payment-fulfillment-validation.md`** is marked PASS with all 27
  items in sections 2–6 unchecked, and its section 1 claims "5 passed, 0 failed" for a 6-test suite
  that was failing one. It also references `PlaywrightFixtureSmokeTests.cs`, which no longer exists.
  The tautological assertion it failed to catch is fixed; the validation record itself is not.
- **`BcWebApplicationFactory` duplication** — analysed and deferred, see companion report.
- **Background services** (`DeferredJobPollerService`, `CronSchedulerService`, `OutboxPollerService`,
  `JobDispatcherService`, `BackgroundMessageDispatcher`) do not swallow `OperationCanceledException`
  on shutdown. Each logs a stack trace at `Error`/`Critical` and trips
  `BackgroundServiceExceptionBehavior.StopHost`. Cosmetic in tests, but it buries real failures.
- **`OrderSummaryPage.OpenPaymentAsync`** reload-polls ten times to paper over eventual consistency.
- **`LoginPage`** has a public constructor where the POM rule calls for a private one plus a static
  `NavigateAsync`.
