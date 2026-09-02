using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class CartOrderTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly ITestOutputHelper _output;

        public CartOrderTests(PlaywrightBrowserFixture browserFixture, ITestOutputHelper output)
        {
            _browserFixture = browserFixture;
            _output = output;
        }

        [Fact]
        public async Task Products_Cart_CustomerForm_CreatesOrderWithoutPayment()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.Sink.SetOutput(_output);
            var services = factory.StartKestrelHost();
            await E2ESeed.CustomerAsync(services);
            var products = await E2ESeed.BasketProductsWithNamesAsync(services);

            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var loginPage = await LoginPage.NavigateAsync(page, factory.ServerAddress);
            await loginPage.LoginAsync(E2ESeed.CustomerEmail, E2ESeed.Password);

            var storefront = await StorefrontPage.NavigateAsync(page, factory.ServerAddress);
            var firstProduct = await storefront.OpenProductAsync(products[0].Name);
            await firstProduct.AddToCartAsync(products[0].Id, 2);

            storefront = await StorefrontPage.NavigateAsync(page, factory.ServerAddress);
            var secondProduct = await storefront.OpenProductAsync(products[1].Name);
            await secondProduct.AddToCartAsync(products[1].Id, 3);

            ICartPage cart = await CartPage.NavigateAsync(page, factory.ServerAddress);
            cart = await cart.ShouldContainProductAsync(products[0].Name, 2);
            await cart.ShouldContainProductAsync(products[1].Name, 3);

            var orderForm = await cart.ProceedToOrderAsync();
            orderForm = await orderForm.FillCustomerAsync();
            var summary = await orderForm.SubmitAsync();

            await summary.ShouldConfirmOrderAsync();
        }
    }
}
