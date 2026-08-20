using Microsoft.Playwright;
using Shouldly;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class OrderSummaryPage : IOrderSummaryPage
    {
        private readonly IPage _page;

        internal OrderSummaryPage(IPage page)
        {
            _page = page;
        }

        public async Task ShouldConfirmOrderAsync()
        {
            (await _page.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(1);
            (await _page.Locator("strong").InnerTextAsync()).ShouldNotBeNullOrWhiteSpace();
        }

        public async Task<int> GetOrderIdAsync()
        {
            var orderIdText = await _page.Locator("strong").InnerTextAsync();
            if (!int.TryParse(orderIdText.Trim(), out var orderId))
            {
                throw new InvalidOperationException($"Order summary contains an invalid order id: '{orderIdText}'.");
            }

            return orderId;
        }

        public async Task<IPaymentPage> OpenPaymentAsync()
        {
            await _page.GetByRole(AriaRole.Link, new() { Name = "Zapłać za zamówienie" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Payments/Create/**");

            int? lastStatus = null;
            var lastBody = string.Empty;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var response = await _page.ReloadAsync();
                lastStatus = response?.Status;
                if (response is not null && response.Ok
                    && await _page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź płatność" }).CountAsync() > 0)
                {
                    return new PaymentPage(_page);
                }

                var body = await _page.Locator("body").InnerTextAsync();
                lastBody = body.Length <= 500 ? body : body[..500];
                await Task.Delay(250);
            }

            throw new InvalidOperationException(
                $"Payment page did not become available after order placement. "
                + $"URL: {_page.Url}; status: {lastStatus?.ToString() ?? "none"}; body: {lastBody}");
        }
    }
}