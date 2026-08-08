using Microsoft.Playwright;
using Shouldly;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class CartPage
    {
        private readonly IPage _page;

        private CartPage(IPage page)
        {
            _page = page;
        }

        public static async Task<CartPage> NavigateAsync(IPage page, string baseAddress)
        {
            await page.GotoAsync($"{baseAddress}/Presale/Checkout/Cart");
            return new CartPage(page);
        }

        public async Task<CartPage> ShouldContainProductAsync(string productName, int quantity)
        {
            var row = _page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = productName });
            (await row.CountAsync()).ShouldBe(1);
            (await row.Locator("span.fw-semibold").InnerTextAsync()).Trim().ShouldBe(quantity.ToString());
            return this;
        }

        public async Task<PlaceOrderPage> ProceedToOrderAsync()
        {
            await _page.GetByRole(AriaRole.Link, new() { Name = "Przejdź do zamówienia" }).ClickAsync();
            await _page.WaitForURLAsync("**/Presale/Checkout/PlaceOrder");
            return new PlaceOrderPage(_page);
        }
    }
}