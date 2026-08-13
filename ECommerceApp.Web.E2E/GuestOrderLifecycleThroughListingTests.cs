using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using ECommerceApp.Web.E2E.Scenarios;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    /// <summary>
    /// Same lifecycle as <see cref="GuestOrderLifecycleTests"/>, but the customer reaches each product
    /// through the storefront listing instead of a direct product URL. Kept as its own class so the two
    /// slowest tests in the suite land in separate collections and run in parallel.
    /// </summary>
    public sealed class GuestOrderLifecycleThroughListingTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly ITestOutputHelper _output;

        public GuestOrderLifecycleThroughListingTests(
            PlaywrightBrowserFixture browserFixture,
            ITestOutputHelper output)
        {
            _browserFixture = browserFixture;
            _output = output;
        }

        [Fact]
        public async Task GuestOrderLifecycle_ProductsFromStorefrontListing_CompletesAcrossBothPersonas()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.CustomerAsync(services);
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.LifecycleProductsAsync(services);

            await using var customerContext = await _browserFixture.Browser.NewContextAsync();
            await using var adminContext = await _browserFixture.Browser.NewContextAsync();
            var customerPage = await customerContext.NewPageAsync();
            var adminPage = await adminContext.NewPageAsync();

            var customerLogin = await LoginPage.NavigateAsync(customerPage, factory.ServerAddress);
            await customerLogin.LoginAsync(E2ESeed.CustomerEmail, E2ESeed.Password);
            var adminLogin = await LoginPage.NavigateAsync(adminPage, factory.ServerAddress);
            await adminLogin.LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            var result = await new GuestOrderLifecycleScenario().ExecuteThroughStorefrontListingAsync(
                customerPage,
                adminPage,
                factory.ServerAddress,
                productIds[0],
                productIds[1]);

            result.PaymentConfirmed.ShouldBeTrue();
            result.FinalShipmentStatus.ShouldBe("Delivered");
        }
    }
}
