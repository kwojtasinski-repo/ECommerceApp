using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using ECommerceApp.Web.E2E.Scenarios;
using Microsoft.Playwright;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class GuestCheckoutLifecycleTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly ITestOutputHelper _output;

        public GuestCheckoutLifecycleTests(PlaywrightBrowserFixture browserFixture, ITestOutputHelper output)
        {
            _browserFixture = browserFixture;
            _output = output;
        }

        [Fact]
        public async Task AnonymousGuestCheckout_PaysAndPromotesAccount()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            await using var adminContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();
            var adminPage = await adminContext.NewPageAsync();

            var adminLogin = await LoginPage.NavigateAsync(adminPage, factory.ServerAddress);
            await adminLogin.LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            var orderId = await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAndPromotionAsync(
                guestPage,
                factory.ServerAddress,
                productIds[0]);

            orderId.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task AnonymousGuestCheckout_PaysAndReachesDelivery()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            await using var adminContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();
            var adminPage = await adminContext.NewPageAsync();

            var adminLogin = await LoginPage.NavigateAsync(adminPage, factory.ServerAddress);
            await adminLogin.LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            var result = await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutThroughDeliveryAsync(
                guestPage,
                adminPage,
                factory.ServerAddress,
                productIds[0]);

            result.PaymentConfirmed.ShouldBeTrue();
            result.FinalShipmentStatus.ShouldBe("Delivered");
        }

        /// <summary>
        /// The real-browser counterpart to the HTTP-level
        /// <c>OrderAccessRecoveryIntegrationTests.SalesOrdersDetails_AnonymousWithGuestAccess_AllowsOwnOrder_RejectsOtherOrder</c>
        /// family: proves the whole pipeline (real Secure/SameSite cookies, real redirect handling, real
        /// GuestAccess sign-in) enforces single-order isolation end to end, not just under AngleSharp
        /// simulation. Deliberately one grounding check, not the full own/other/no-order matrix — that
        /// matrix already lives in the Integration tier per this project's own testing-tier split (no
        /// JS/async-timing concern here, so Playwright buys nothing beyond proving the real thing works).
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_CannotOpenAnotherGuestsOrderSummary()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestAContext = await _browserFixture.Browser.NewContextAsync();
            await using var guestBContext = await _browserFixture.Browser.NewContextAsync();
            var guestAPage = await guestAContext.NewPageAsync();
            var guestBPage = await guestBContext.NewPageAsync();

            var scenario = new GuestOrderLifecycleScenario();
            var orderIdA = await scenario.ExecuteAnonymousCheckoutAsync(guestAPage, factory.ServerAddress, productIds[0]);
            var orderIdB = await scenario.ExecuteAnonymousCheckoutAsync(guestBPage, factory.ServerAddress, productIds[0]);

            // guestAPage is GuestAccess-signed-in scoped to order A; try to open B's confirmation directly.
            await guestAPage.GotoAsync($"{factory.ServerAddress}/Presale/Checkout/Summary/{orderIdB}");

            guestAPage.Url.ShouldContain($"/Presale/Checkout/Order/{orderIdB}",
                customMessage: "a guest's GuestAccess ticket for their own order must not unlock a different guest's order confirmation page");
            (await guestAPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(0, "order B's confirmation content must never render for guest A");

            orderIdA.ShouldNotBe(orderIdB);
        }

        /// <summary>
        /// The other cross-persona direction from <see cref="AnonymousGuestCheckout_CannotOpenAnotherGuestsOrderSummary"/>:
        /// a guest with their own active GuestAccess ticket trying a *registered customer's* order, not
        /// just another guest's. Same underlying <c>OrderAccessAuthorizationHandler</c> ownership check
        /// either way, but the denial path is different and worth pinning down explicitly: for another
        /// guest's order, <c>CheckoutController.Order</c> shows the self-service email+code lookup form
        /// (the order's <c>UserId</c> starts with <c>gst_</c>); for a registered customer's order it
        /// recognizes the owning account is real and sends the caller to the actual Login page instead —
        /// there is no guest self-service path onto a real account. Found by running this test first with
        /// the guest-vs-guest assertion and observing the real redirect chain land on Login instead.
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_CannotOpenRegisteredCustomersOrderSummary()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.CustomerAsync(services);
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            await using var customerContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();
            var customerPage = await customerContext.NewPageAsync();

            var customerLogin = await LoginPage.NavigateAsync(customerPage, factory.ServerAddress);
            await customerLogin.LoginAsync(E2ESeed.CustomerEmail, E2ESeed.Password);

            var scenario = new GuestOrderLifecycleScenario();
            var guestOrderId = await scenario.ExecuteAnonymousCheckoutAsync(guestPage, factory.ServerAddress, productIds[0]);
            var customerOrderId = await scenario.ExecuteRegisteredCustomerCheckoutAsync(customerPage, factory.ServerAddress, productIds[0]);

            await guestPage.GotoAsync($"{factory.ServerAddress}/Presale/Checkout/Summary/{customerOrderId}");

            guestPage.Url.ShouldContain("/Identity/Account/Login",
                customMessage: "a guest's GuestAccess ticket must not unlock a registered customer's order — and since that order has no guest self-service path, the denial must cascade all the way to the real Login page");
            (await guestPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(0, "the customer's confirmation content must never render for the guest");

            guestOrderId.ShouldNotBe(customerOrderId);
        }

        /// <summary>
        /// The vice versa of <see cref="AnonymousGuestCheckout_CannotOpenRegisteredCustomersOrderSummary"/>:
        /// a real logged-in customer trying a guest's order. Denial for an <c>Identity.Application</c>
        /// caller is a genuine <c>Forbid()</c> (landing on AccessDenied), not the lookup-page redirect a
        /// guest/anonymous caller gets — see <c>OrderAccessDenial.Result</c> — so this exercises the
        /// other branch of that same decision point, not a duplicate assertion of the first test.
        /// </summary>
        [Fact]
        public async Task RegisteredCustomer_CannotOpenAnonymousGuestsOrderSummary()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.CustomerAsync(services);
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            await using var customerContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();
            var customerPage = await customerContext.NewPageAsync();

            var customerLogin = await LoginPage.NavigateAsync(customerPage, factory.ServerAddress);
            await customerLogin.LoginAsync(E2ESeed.CustomerEmail, E2ESeed.Password);

            var guestOrderId = await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAsync(
                guestPage, factory.ServerAddress, productIds[0]);

            await customerPage.GotoAsync($"{factory.ServerAddress}/Presale/Checkout/Summary/{guestOrderId}");

            customerPage.Url.ShouldContain("AccessDenied",
                customMessage: "a real registered customer must be forbidden from a guest's order, not shown its confirmation");
            (await customerPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(0, "the guest's confirmation content must never render for the customer");
        }

        /// <summary>
        /// Generalizes <see cref="AnonymousGuestCheckout_CannotReachIdentityManage"/> beyond Identity/Manage:
        /// AccountProfile/Profile is a second, independent area kept off the CustomerOrGuest policy (see
        /// <c>AppAuthorizationPoliciesTests.ProfileController_UsesBareAuthorize_NotCustomerOrGuest</c> for
        /// the unit-level proof it's wired that way) — this proves a real GuestAccess-ticketed guest
        /// actually gets turned away from it in the live pipeline too, not just Identity/Manage.
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_CannotReachAccountProfile()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();

            await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAsync(
                guestPage, factory.ServerAddress, productIds[0]);
            // guestPage is now GuestAccess-signed-in — an account-management action, not just a page view.

            await guestPage.GotoAsync($"{factory.ServerAddress}/AccountProfile/Profile");

            guestPage.Url.ShouldContain("/Identity/Account/Login",
                customMessage: "a GuestAccess-authenticated guest must not reach AccountProfile, an area reserved for real accounts");
            (await new LoginPage(guestPage).IsDisplayed()).ShouldBeTrue();
        }

        /// <summary>
        /// The strictest tier of the persona ladder: a guest is scoped inside <c>CustomerOrGuest</c>
        /// controllers to their own order via <c>OrderAccessAuthorizationHandler</c>, but those same
        /// controllers carry per-action <c>[Authorize(Roles = MaintenanceRole)]</c> overrides for the
        /// staff-only actions (the all-orders admin list, at <c>Sales/Orders/Index</c>) — a guest must
        /// not reach those even though the controller as a whole accepts GuestAccess. A GuestAccess
        /// principal never carries a role claim (<c>SignInGuestAccessAsync</c> only sets NameIdentifier
        /// and the order-scope claims), so this proves that generalizes correctly in the real pipeline:
        /// guest &lt; real customer &lt; staff, not guest == real customer inside these controllers.
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_CannotReachMaintenanceOnlyOrdersList()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();

            await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAsync(
                guestPage, factory.ServerAddress, productIds[0]);
            // guestPage is now GuestAccess-signed-in — satisfies the controller's class-level policy,
            // but not the action-level MaintenanceRole requirement below.

            await guestPage.GotoAsync($"{factory.ServerAddress}/Sales/Orders/Index");

            (await guestPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienia", Exact = true }).CountAsync())
                .ShouldBe(0, "a guest must never see the staff all-orders admin list, regardless of the controller's own-order access");
            guestPage.Url.ShouldNotContain("/Sales/Orders/Index",
                customMessage: "the guest must be turned away from the admin route entirely, not shown a filtered version of it");
        }

        /// <summary>
        /// The real-browser counterpart to the HTTP-level (TestServer/AngleSharp-style)
        /// <c>SessionIsolationTests.GuestAccessSession_CannotReachIdentityManage</c> and the unit-level
        /// <c>AppAuthorizationPoliciesTests</c>. Those two prove the authorization *configuration* is
        /// correct in isolation; this proves the real Kestrel-hosted pipeline — real Secure/SameSite
        /// GuestAccess cookie, real ASP.NET Core Identity redirect chain, real browser following it —
        /// actually keeps a guest out of account management end to end. If any of Startup.cs's
        /// AddAuthorization wiring, the GuestAccess cookie's real attributes, or the Identity/Manage
        /// Razor Pages convention ever drift out of sync with each other, only a real browser hitting the
        /// real host would catch it — that's the gap this test closes, deliberately paying the slower
        /// browser-test cost for it because this is a real-account-vs-guest data boundary, not a
        /// convenience feature.
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_CannotReachIdentityManage()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();

            var orderId = await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAsync(
                guestPage, factory.ServerAddress, productIds[0]);
            // guestPage is now GuestAccess-signed-in (not anonymous) — the actual thing under test.

            await guestPage.GotoAsync($"{factory.ServerAddress}/Identity/Account/Manage");

            guestPage.Url.ShouldContain("/Identity/Account/Login",
                customMessage: "a GuestAccess-authenticated guest must be redirected to Login by the real pipeline, not let into account management");
            (await new LoginPage(guestPage).IsDisplayed()).ShouldBeTrue(
                "the login form must actually be what's rendered, not merely a URL that happens to contain 'Login'");

            orderId.ShouldBeGreaterThan(0);
        }

        /// <summary>
        /// Completes the persona matrix alongside <see cref="AnonymousGuestCheckout_CannotReachIdentityManage"/>:
        /// that test proves a guest who *has* a GuestAccess ticket is still kept out of account
        /// management. This proves the weaker persona — someone who never checked out at all, no
        /// GuestAccess cookie, no Identity cookie, nothing — is kept out too, in a real browser. Not
        /// redundant with the anonymous-cookieless HTTP-level
        /// <c>SessionIsolationTests.AnonymousCaller_CannotReachIdentityManage</c>: that proves the
        /// TestServer pipeline denies it, this proves the real Kestrel-hosted one does too.
        /// </summary>
        [Fact]
        public async Task AnonymousVisitor_CannotReachIdentityManage()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            factory.StartKestrelHost();

            await using var anonymousContext = await _browserFixture.Browser.NewContextAsync();
            var anonymousPage = await anonymousContext.NewPageAsync();

            await anonymousPage.GotoAsync($"{factory.ServerAddress}/Identity/Account/Manage");

            anonymousPage.Url.ShouldContain("/Identity/Account/Login",
                customMessage: "a caller with no session at all must be redirected to Login by the real pipeline");
            (await new LoginPage(anonymousPage).IsDisplayed()).ShouldBeTrue();
        }

        /// <summary>
        /// The other half of the anonymous-persona matrix: a visitor who never checked out (no
        /// GuestAccess ticket, no guest cookie at all — a brand-new browser context) must not be able to
        /// open somebody else's already-placed order by guessing/typing its id, the same way a guest
        /// with their *own* GuestAccess ticket can't (<see cref="AnonymousGuestCheckout_CannotOpenAnotherGuestsOrderSummary"/>).
        /// Summary stays <c>[AllowAnonymous]</c> by design (ADR-0030 §11 — a nonexistent/unowned order id
        /// still routes to the lookup page, not a login wall a guest has no password for), so the
        /// meaningful assertion is that the order's confirmation content never renders, not the response
        /// code.
        /// </summary>
        [Fact]
        public async Task AnonymousVisitor_CannotOpenExistingGuestOrderSummary()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var ownerContext = await _browserFixture.Browser.NewContextAsync();
            var ownerPage = await ownerContext.NewPageAsync();
            var orderId = await new GuestOrderLifecycleScenario().ExecuteAnonymousCheckoutAsync(
                ownerPage, factory.ServerAddress, productIds[0]);

            // Brand-new context: no GuestSession cookie, no GuestAccess ticket, nothing.
            await using var anonymousContext = await _browserFixture.Browser.NewContextAsync();
            var anonymousPage = await anonymousContext.NewPageAsync();

            await anonymousPage.GotoAsync($"{factory.ServerAddress}/Presale/Checkout/Summary/{orderId}");

            anonymousPage.Url.ShouldContain($"/Presale/Checkout/Order/{orderId}",
                customMessage: "a caller with no session at all must not see someone else's order confirmation, routed to the lookup page instead");
            (await anonymousPage.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(0, "the owning guest's confirmation content must never render for a cookie-less visitor");
        }

        /// <summary>
        /// The positive control for the whole persona matrix: without this, the three denial tests above
        /// can't be told apart from a bug that redirects *everyone* to Login regardless of identity. A
        /// real signed-in account must still reach the area the other tests prove guests and anonymous
        /// visitors are kept out of.
        /// </summary>
        [Fact]
        public async Task RegisteredCustomer_CanReachIdentityManage()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);

            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var login = await LoginPage.NavigateAsync(page, factory.ServerAddress);
            await login.LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            await page.GotoAsync($"{factory.ServerAddress}/Identity/Account/Manage");

            page.Url.ShouldNotContain("/Identity/Account/Login",
                customMessage: "a real signed-in account must not be bounced to Login the way a guest/anonymous caller is");
            (await new LoginPage(page).IsDisplayed()).ShouldBeFalse(
                "the rendered page must be account management, not the login form");
        }

        /// <summary>
        /// ADR-0030 Phase 9 replaced Phase 7/8's admin-Backoffice-assisted recovery magic link with a
        /// self-service email+code flow on the unified order-lookup page — see
        /// <see cref="GuestOrderLifecycleScenario.ExecuteAnonymousSelfServiceRecoveryAsync"/> for the
        /// full rationale. This test supersedes the old
        /// AnonymousGuestCheckout_LostCookie_RecoversOrderAccessThroughBackoffice scenario (which
        /// exercised UI that no longer exists), not a silently dropped one.
        /// </summary>
        [Fact]
        public async Task AnonymousGuestCheckout_LostCookie_RecoversOrderAccessThroughSelfService()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var guestContext = await _browserFixture.Browser.NewContextAsync();
            await using var adminContext = await _browserFixture.Browser.NewContextAsync();
            var guestPage = await guestContext.NewPageAsync();
            var adminPage = await adminContext.NewPageAsync();

            var adminLogin = await LoginPage.NavigateAsync(adminPage, factory.ServerAddress);
            await adminLogin.LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            var orderId = await new GuestOrderLifecycleScenario().ExecuteAnonymousSelfServiceRecoveryAsync(
                guestPage,
                adminPage,
                factory.ServerAddress,
                productIds[0]);

            orderId.ShouldBeGreaterThan(0);
        }
    }
}
