using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class OrderPlacementFailedHandler : IMessageHandler<OrderPlacementFailed>
    {
        private readonly IStockService _stockService;

        public OrderPlacementFailedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(OrderPlacementFailed message, CancellationToken ct = default)
        {
            foreach (var item in message.Items)
            {
                await _stockService.ReleaseAsync(message.OrderId, item.ProductId, item.Quantity, ct);
            }
        }
    }
}
