using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class OrderAccessLookupPage : IOrderAccessLookupPage
    {
        private readonly IPage _page;

        private OrderAccessLookupPage(IPage page)
        {
            _page = page;
        }

        public static async Task<OrderAccessLookupPage> NavigateAsync(IPage page, string baseAddress, int orderId)
        {
            await page.GotoAsync($"{baseAddress}/Presale/Checkout/Order/{orderId}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new OrderAccessLookupPage(page);
        }

        public async Task<IOrderAccessLookupPage> RequestAccessAsync(string email)
        {
            await _page.Locator("#email").FillAsync(email);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Przygotuj kod" }).ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return this;
        }

        public async Task<IOrderSummaryPage> ConfirmAccessAsync(string code)
        {
            await _page.Locator("#code").FillAsync(code);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź dostęp" }).ClickAsync();
            await _page.WaitForURLAsync("**/Presale/Checkout/Summary/*");
            return new OrderSummaryPage(_page);
        }

        public async Task<string> GetMessageAsync()
        {
            var alert = _page.Locator(".alert-info");
            return await alert.CountAsync() > 0 ? (await alert.InnerTextAsync()).Trim() : string.Empty;
        }
    }
}
