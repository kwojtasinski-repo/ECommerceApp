using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ShipmentFailedHandler : IMessageHandler<ShipmentFailed>
    {
        private readonly IStockService _stockService;

        public ShipmentFailedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(ShipmentFailed message, CancellationToken ct = default)
        {
            foreach (var item in message.Items)
            {
                await _stockService.ReleaseAsync(message.OrderId, item.ProductId, item.Quantity, ct);
            }
        }
    }
}
