using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class ShipmentCreatePage : IShipmentCreatePage
    {
        private readonly IPage _page;

        internal ShipmentCreatePage(IPage page)
        {
            _page = page;
        }

        public async Task<IOrderShipmentsPage> CreateShipmentAsync()
        {
            await _page.GetByRole(AriaRole.Button, new() { Name = "Utwórz przesyłkę" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/OrderShipments**");
            return new OrderShipmentsPage(_page);
        }
    }
}