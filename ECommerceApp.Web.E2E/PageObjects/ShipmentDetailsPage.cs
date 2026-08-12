using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class ShipmentDetailsPage : IShipmentDetailsPage
    {
        private readonly IPage _page;

        internal ShipmentDetailsPage(IPage page)
        {
            _page = page;
        }

        public async Task<IShipmentDetailsPage> DispatchAsync()
        {
            await RequireStatusAsync("Pending", "#trackingNumber");
            await _page.GetByRole(AriaRole.Button, new() { Name = "Wyślij" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/Details/**");
            return new ShipmentDetailsPage(_page);
        }

        public async Task<IShipmentDetailsPage> DeliverAsync()
        {
            await RequireStatusAsync("InTransit", null);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Oznacz dostarczono" }).ClickAsync();
            await _page.WaitForURLAsync("**/Sales/Shipment/Details/**");
            return new ShipmentDetailsPage(_page);
        }

        public async Task<string> GetStatusAsync()
        {
            var renderedStatus = (await StatusBadge().InnerTextAsync()).Trim();
            return renderedStatus switch
            {
                "Oczekuje" => "Pending",
                "W drodze" => "InTransit",
                "Dostarczono" => "Delivered",
                "Niepowodzenie" => "Failed",
                "Częściowo dostarczono" => "PartiallyDelivered",
                _ => renderedStatus
            };
        }

        private async Task RequireStatusAsync(string expectedStatus, string requiredSelector)
        {
            var actualStatus = await GetStatusAsync();
            var statusMatches = actualStatus switch
            {
                "Pending" => expectedStatus == "Pending",
                "InTransit" => expectedStatus == "InTransit",
                _ => false
            };

            if (!statusMatches || (requiredSelector is not null && await _page.Locator(requiredSelector).CountAsync() == 0))
            {
                throw new InvalidOperationException(
                    $"Shipment state mismatch. Expected '{expectedStatus}', rendered status '{actualStatus.Trim()}'.");
            }
        }

        private ILocator StatusBadge() => _page.Locator("dd .badge").First;
    }
}