using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class PaymentPage : IPaymentPage
    {
        private readonly IPage _page;

        internal PaymentPage(IPage page)
        {
            _page = page;
        }

        public async Task ConfirmPaymentAsync()
        {
            await _page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź płatność" }).ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            if (_page.Url.Contains("/Sales/Payments/Create", StringComparison.OrdinalIgnoreCase))
            {
                var message = await _page.Locator("[asp-validation-summary], .validation-summary-errors, body").First.InnerTextAsync();
                throw new InvalidOperationException($"Payment confirmation failed. Page message: {message}");
            }
        }
    }
}