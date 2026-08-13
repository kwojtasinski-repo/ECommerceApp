using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class OrderFulfillmentPage : IOrderFulfillmentPage
    {
        private readonly IPage _page;

        private OrderFulfillmentPage(IPage page)
        {
            _page = page;
        }

        public static async Task<OrderFulfillmentPage> NavigateAsync(IPage page, string baseAddress, int orderId)
        {
            await page.GotoAsync($"{baseAddress}/Sales/Orders/Fulfillment?id={orderId}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new OrderFulfillmentPage(page);
        }

        // Matched by its label rather than by position, so adding a row to the definition list above
        // it in Fulfillment.cshtml cannot silently start reading a different field.
        public async Task<string> GetOrderStatusAsync()
            => (await _page.Locator("dt:text-is('Status') + dd").InnerTextAsync()).Trim();

        public async Task<IShipmentCreatePage> OpenCreateShipmentAsync()
        {
            await _page.GetByRole(AriaRole.Link, new() { Name = "Utwórz przesyłkę" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/Create**");
            return new ShipmentCreatePage(_page);
        }
    }
}