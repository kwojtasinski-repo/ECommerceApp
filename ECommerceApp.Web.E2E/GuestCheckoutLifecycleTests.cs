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
