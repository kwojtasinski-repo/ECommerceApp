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
| `PlaywrightBrowserFixture` | `ICollectionFixture`, one per test run (via `[Collection(PlaywrightCollection.Name)]`) | Owns the expensive `IPlaywright`/`IBrowser` (Chromium, headless). Never create a second one. |
| `PlaywrightWebApplicationFactory` | `IClassFixture`, one per test class | Hosts `ECommerceApp.Web` on a real, dynamically-allocated Kestrel port. InMemory DB for all BC DbContexts, `ICategoryService` stubbed (known EF InMemory / `CategoryName` value-object limitation — do not try to "fix" this by touching Catalog code), **real** `IMessageBroker` + `BackgroundMessageDispatcher` + `OutboxPollerService` (short `OutboxPollInterval`, see `appsettings.test.json`). |
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
    [Collection(PlaywrightCollection.Name)]
    public sealed class {{Flow}}Tests : IClassFixture<PlaywrightWebApplicationFactory>
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly PlaywrightWebApplicationFactory _factory;

        public {{Flow}}Tests(PlaywrightBrowserFixture browserFixture, PlaywrightWebApplicationFactory factory)
        {
            _browserFixture = browserFixture;
            _factory = factory;
        }

        [Fact]
        public async Task {{Method}}_{{Conditions}}_{{ExpectedResult}}()
        {
            _factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync($"{_factory.ServerAddress}/{{Route}}");

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

## Locators (once Page Object Models exist)

No Page Object Model / Component / Scenario layer exists yet in this project (planned, not yet
built as of this skill's writing). Until then, write `page.Locator(...)` calls directly in the test.
When the POM layer lands: locators must always be hidden behind a method on the page/component
object, never exposed as a public selector string — check whether this skill has been updated with
the concrete base classes before scaffolding new tests, and prefer reusing them over writing raw
`page.Locator(...)` again.

## Related project rules

- `.github/instructions/integration-web-e2e-testing.instructions.md` — "Browser E2E tier" section
- `.github/instructions/testing.instructions.md`
- `diagnose-integration-e2e-tests` — when a test in this project fails
- `create-integration-test` — for the non-browser, synchronous-broker tiers instead
