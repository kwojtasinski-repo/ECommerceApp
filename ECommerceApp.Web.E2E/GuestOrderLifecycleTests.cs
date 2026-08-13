using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using ECommerceApp.Web.E2E.Scenarios;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class GuestOrderLifecycleTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly ITestOutputHelper _output;

        public GuestOrderLifecycleTests(PlaywrightBrowserFixture browserFixture, ITestOutputHelper output)
        {
            _browserFixture = browserFixture;
            _output = output;
        }

        [Fact]
        public async Task GuestOrderLifecycle_ProductsToDelivery_CompletesAcrossBothPersonas()
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

            var result = await new GuestOrderLifecycleScenario().ExecuteAsync(
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
