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

        public async Task<IShipmentCreatePage> OpenCreateShipmentAsync()
        {
            await _page.GetByRole(AriaRole.Link, new() { Name = "Utwórz przesyłkę" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/Create**");
            return new ShipmentCreatePage(_page);
        }
    }
}