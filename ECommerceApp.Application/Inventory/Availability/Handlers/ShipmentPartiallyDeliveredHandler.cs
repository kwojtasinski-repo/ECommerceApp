using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ShipmentPartiallyDeliveredHandler : IMessageHandler<ShipmentPartiallyDelivered>
    {
        private readonly IStockService _stockService;

        public ShipmentPartiallyDeliveredHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            foreach (var item in message.DeliveredItems)
            {
                await _stockService.FulfillAsync(message.OrderId, item.ProductId, item.Quantity, ct);
            }

            foreach (var item in message.FailedItems)
            {
                await _stockService.ReleaseAsync(message.OrderId, item.ProductId, item.Quantity, ct);
            }
        }
    }
}
