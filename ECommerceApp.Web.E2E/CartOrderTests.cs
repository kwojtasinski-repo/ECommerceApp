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
            var productIds = await E2ESeed.BasketProductsAsync(services);

            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var loginPage = await LoginPage.NavigateAsync(page, factory.ServerAddress);
            await loginPage.LoginAsync(E2ESeed.CustomerEmail, E2ESeed.Password);

            var storefront = await StorefrontPage.NavigateAsync(page, factory.ServerAddress);
            var firstProduct = await storefront.OpenProductAsync("E2E Basket Product A");
            await firstProduct.AddToCartAsync(productIds[0], 2);

            storefront = await StorefrontPage.NavigateAsync(page, factory.ServerAddress);
            var secondProduct = await storefront.OpenProductAsync("E2E Basket Product B");
            await secondProduct.AddToCartAsync(productIds[1], 3);

            ICartPage cart = await CartPage.NavigateAsync(page, factory.ServerAddress);
            cart = await cart.ShouldContainProductAsync("E2E Basket Product A", 2);
            await cart.ShouldContainProductAsync("E2E Basket Product B", 3);

            var orderForm = await cart.ProceedToOrderAsync();
            orderForm = await orderForm.FillCustomerAsync();
            var summary = await orderForm.SubmitAsync();

            await summary.ShouldConfirmOrderAsync();
        }
    }
}
