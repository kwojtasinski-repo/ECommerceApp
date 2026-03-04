using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class OrderShippedHandler : IMessageHandler<OrderShipped>
    {
        private readonly IStockService _stockService;

        public OrderShippedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(OrderShipped message, CancellationToken ct = default)
        {
            foreach (var item in message.Items)
            {
                await _stockService.FulfillAsync(message.OrderId, item.ProductId, item.Quantity, ct);
            }
        }
    }
}
