using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class OrderShipmentsPage : IOrderShipmentsPage
    {
        private readonly IPage _page;

        internal OrderShipmentsPage(IPage page)
        {
            _page = page;
        }

        public async Task<IShipmentDetailsPage> OpenLatestShipmentDetailsAsync()
        {
            var detailsLink = _page.Locator("tbody tr").First.GetByRole(
                AriaRole.Link,
                new() { Name = "Szczegóły" });
            if (await detailsLink.CountAsync() == 0)
            {
                throw new InvalidOperationException("No shipment was rendered for the order.");
            }

            await detailsLink.ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/Details/**");
            return new ShipmentDetailsPage(_page);
        }
    }
}