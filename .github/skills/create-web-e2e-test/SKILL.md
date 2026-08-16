---
name: create-web-e2e-test
description: >
  Scaffold a browser-driven E2E test in ECommerceApp.Web.E2E using Playwright against a real
  Kestrel-hosted instance of ECommerceApp.Web. Use when a flow must be observed the way a real
  browser sees it — real JavaScript execution, real page reloads — AND the async message broker /
  Outbox poller timing matters (e.g. a page may render before an event has finished processing).
  Do not use for routing/validation/model-binding checks with no JS or timing concern — that is
  ECommerceApp.Web.IntegrationTests (AngleSharp, synchronous broker, no browser).
argument-hint: "<flow or page under test>"
---

# Create a Web E2E (Playwright) test

`ECommerceApp.Web.E2E` exists for one specific gap the other test tiers deliberately don't cover:
watching what a real browser actually renders while the message broker and Outbox poller are
running **asynchronously**, exactly as they do in production. Every other integration/Web tier in
this repo runs the broker **synchronously** on purpose, for easy assertions — see
`integration-web-e2e-testing.instructions.md`. This tier trades that convenience for realism.

## When to use

- The test must exercise real browser JavaScript (a `fetch()` call, a client-side redirect,
  `location.reload()`) — not just server-rendered HTML.
- The test must prove a page behaves correctly when an async-dispatched event (in-process Channel
  *or* the DB-backed Outbox poller) has **not yet** been processed at render time — the "event not
  processed, page already shown" class of bug.

## When not to use

- Routing, model binding, validation, or authorization checks with no JS/timing concern → use
  `ECommerceApp.Web.IntegrationTests` (`create-integration-test`-adjacent, AngleSharp-based,
  synchronous broker, much faster).
- Service-level DI/handler/cross-BC message tests → `ECommerceApp.IntegrationTests`
  (`BcWebApplicationFactory`/`BcBaseTest<T>`, synchronous `SynchronousMultiHandlerBroker`).
- A test is failing and needs diagnosis, not scaffolding → `diagnose-integration-e2e-tests`.

## The fixture pieces (all in `ECommerceApp.Web.E2E/Infrastructure/`)

| Class | Lifetime | Purpose |
|---|---|---|
| `PlaywrightBrowserFixture` | `[assembly: AssemblyFixture(...)]`, one per test run | Owns the expensive `IPlaywright`/`IBrowser` (Chromium, headless). Never create a second one. Deliberately **not** an `ICollectionFixture`: a collection is xunit's unit of parallelism, so sharing it that way would force every browser test into one collection and serialize the suite. |
| `PlaywrightWebApplicationFactory` | `using var`, one per **test**, never a fixture | Hosts `ECommerceApp.Web` on a real, dynamically-allocated Kestrel port. InMemory DB for all BC DbContexts, `ICategoryService` stubbed (known EF InMemory / `CategoryName` value-object limitation — do not try to "fix" this by touching Catalog code), **real** `IMessageBroker` + `BackgroundMessageDispatcher` + `OutboxPollerService` (short `OutboxPollInterval`, see `appsettings.test.json`). |
| `OutboxDispatchWatcher` | Constructed per use (not a singleton, not a static helper) | Polls `IOutboxRepository.GetSinceAsync` until a specific message type/predicate reaches `OutboxStatus.Dispatched`, or throws `TimeoutException` with the candidates it actually saw. |

## Non-negotiable rule: only use `StartKestrelHost()`

```csharp
var services = _factory.StartKestrelHost();   // idempotent — safe to call every test
var address = _factory.ServerAddress;         // e.g. http://127.0.0.1:53214
```

**Never touch the inherited `_factory.Services`, `_factory.CreateClient()`, or `_factory.Server`.**
In this repo's .NET 10 `Microsoft.AspNetCore.Mvc.Testing`, `WebApplicationFactory.StartServer()`
unconditionally expects a `TestServer` — it cannot be redirected to the real Kestrel host (verified:
overriding `CreateHost` to call `UseKestrel()` throws `InvalidCastException`). Touching those
inherited members lazily builds a **second, completely independent** TestServer-backed host with
its own InMemory database that Playwright never talks to. Any assertion made through it will
silently diverge from what the browser actually did. `StartKestrelHost()` and `ServerAddress` are
the only supported entry points — this is documented on the class itself too.

## Template

```csharp
using ECommerceApp.Web.E2E.Infrastructure;
using Shouldly;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    // No [Collection]: one class = one collection = one unit of parallelism. Give a slow flow its
    // own class so it runs alongside the others rather than behind them.
    public sealed class {{Flow}}Tests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;

        public {{Flow}}Tests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task {{Method}}_{{Conditions}}_{{ExpectedResult}}()
        {
            // Own host per test: own Kestrel port, own IAM and bounded-context InMemory databases.
            // This is what makes the classes safe to run in parallel — do not hoist it into a fixture.
            using var factory = new PlaywrightWebApplicationFactory();
            factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync($"{factory.ServerAddress}/{{Route}}");

            // ... interact / assert via page.Locator(...) ...
        }
    }
}
```

For a flow that publishes an Outbox message and must prove the page behaves correctly both
*before* and *after* dispatch:

```csharp
var services = _factory.StartKestrelHost();
using var scope = services.CreateScope();
var watcher = new OutboxDispatchWatcher(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());
var sinceUtc = DateTime.UtcNow;

// ... trigger the action via the browser (or directly via a resolved service) ...

// Assert the "not yet processed" UI state here, before waiting.

await watcher.WaitForDispatchedAsync<TMessage>(sinceUtc, m => /* predicate */, TimeSpan.FromSeconds(10));

// Assert the "processed" UI state here (e.g. after page.ReloadAsync()).
```

## Reading JavaScript instead of executing it

This project uses AngleSharp-free, real-browser navigation — but there is **no** headless-JS
polyfill layer (no Jint/AngleSharp.Js). If a flow's `<script>` block does something simple (a fixed
`fetch()` URL with a static body, then `location.reload()`), it's fine to let Playwright click the
real element and let the real script run — that's the whole point of this tier. Do not try to
"shortcut" by POSTing to the endpoint directly from test code instead of clicking; that reintroduces
exactly the gap (untested JS wiring) this tier exists to close.

## Page Objects, Components, and Scenarios

This project uses a composition-first test-support model. The reference implementation is
`ECommerceApp.Web.E2E/PageObjects/LoginPage.cs`, with its narrow `ILoginPage` contract. A POM is a
sealed class that owns a private `IPage` reference for one view. Locators and the underlying page
are never public. When a POM already exists for a page, new tests must use or extend that POM;
do not add raw `page.Locator(...)` calls for behavior the POM already owns.

The hierarchy is `Scenario -> POM -> Component`:

- A Component is a private implementation detail of a POM, scoped to a root `ILocator`. A POM may
  return a narrow component/modal interface only when the caller genuinely needs to operate on a
  same-page fragment. A modal may expose `Task<bool> IsOpenAsync()` when a shared UI-state check is
  useful. Do not use `IDisposable` to mean that a modal closed; Playwright resources belong to the
  test/session owner.
- A Scenario composes POM interfaces, never hidden Components or raw `IPage`. It represents a
  repeatable business intention, is not a catch-all workflow object, and returns a small immutable
  business result rather than a POM. Detailed locator and expected-error assertions belong to the
  relevant POM/component unless an explicit flow-level exception is agreed.
- For redirect, popup, new tab, or new window, the test/Scenario owns the Playwright event and page
  lifetime, then constructs the next POM. A POM must not hide context creation, popup watchers, or
  page selection. For a same-page modal, the POM owns `Open...` and may return the modal/component
  interface.

The reference Scenario is
[`GuestOrderLifecycleScenario`](../../../../ECommerceApp.Web.E2E/Scenarios/GuestOrderLifecycleScenario.cs).
It composes storefront, checkout, payment, shipment creation, and shipment-state POMs, receives
already-authenticated pages from its host, and returns the immutable `OrderLifecycleResult`.
It does not create browser contexts or perform login.

The same class also holds the true anonymous, no-prior-login coverage for ADR-0030 (guest
checkout): `ExecuteAnonymousCheckoutAndPromotionAsync` (cart → guest `PlaceOrder` → payment →
account promotion, driven by
[`GuestCheckoutLifecycleTests.AnonymousGuestCheckout_PaysAndPromotesAccount`](../../../../ECommerceApp.Web.E2E/GuestCheckoutLifecycleTests.cs))
and `ExecuteAnonymousCookieRecoveryAsync` (cookie loss → order-access recovery through the login
page and the admin Backoffice view, driven by the same test class's
`AnonymousGuestCheckout_LostCookie_RecoversOrderAccessThroughBackoffice`). Both use a guest
`BrowserContext` alongside a separately authenticated admin `BrowserContext` — the two-persona-
plus-admin pattern for a workflow that changes persona, per the rule below.

Same-tab server redirects that keep using the same `IPage` may be followed by a POM constructing
the next POM. This applies to ordinary checkout and shipment redirects. It does not transfer
ownership of popups, new tabs, new windows, or persona changes to a POM. A workflow that changes
persona must use a separate `BrowserContext` and `IPage`, both created and authenticated by the
test or Scenario host.

POM actions may return `Task` or a narrow `Task<IPom>` when the action remains on the same surface
and fluent chaining helps readability. A redirecting action normally returns `Task` so the host can
observe the new surface. The call-site form for a same-page fluent operation is:

```csharp
var loginPage = await LoginPage.NavigateAsync(page, serverAddress);
loginPage = await loginPage.SubmitLogin(email, password);
```

An inline nested await is acceptable for one follow-up operation:

```csharp
var loginPage = await (await LoginPage.NavigateAsync(page, serverAddress))
    .SubmitLogin(email, password);
```

Prefer `Task`/`Task<T>` for Playwright operations. Never use `async void`; a deliberately delayed
operation must retain its `Task` and await it later. `IBrowser` may be shared by the fixture, but
`BrowserContext`, `IPage`, POMs, Components, and Scenarios are per test/session and are never
singletons. Do not introduce a global `GetService<T>()` resolver until a real Scenario demonstrates
the required factory or session lifetime.

## Related project rules

- `.github/instructions/integration-web-e2e-testing.instructions.md` — "Browser E2E tier" section
- `.github/instructions/testing.instructions.md`
- `diagnose-integration-e2e-tests` — when a test in this project fails
- `create-integration-test` — for the non-browser, synchronous-broker tiers instead
