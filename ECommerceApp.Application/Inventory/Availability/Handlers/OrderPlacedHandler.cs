using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
    {
        private readonly IStockService _stockService;

        public OrderPlacedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            foreach (var item in message.Items)
            {
                var dto = new ReserveStockDto(
                    item.ProductId,
                    message.OrderId,
                    item.Quantity,
                    message.UserId,
                    message.ExpiresAt);

                await _stockService.ReserveAsync(dto, ct);
            }
        }
    }
}
