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

            // The Payment row this page waits for is created asynchronously by
            // Sales.Payments.Handlers.OrderPlacedHandler reacting to the OrderPlaced outbox message, not
            // synchronously with order placement — so this has always been a poll, not a one-shot check.
            // 10x250ms (2.5s) was tuned against a private InMemory Outbox with nothing else competing for
            // it. On the shared SQL Server Outbox (phase 22) every concurrently running test's messages
            // sit in the same queue, so dispatch of any one message can legitimately take longer; budget
            // matches OutboxDispatchWatcher's own default timeout for the same pipeline elsewhere in this
            // suite instead of the old private-store number.
            int? lastStatus = null;
            var lastBody = string.Empty;
            for (var attempt = 0; attempt < 80; attempt++)
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