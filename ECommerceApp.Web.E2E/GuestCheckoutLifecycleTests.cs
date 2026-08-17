using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using ECommerceApp.Web.E2E.Scenarios;
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
