using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentDeliveredHandler : IMessageHandler<ShipmentDelivered>
    {
        private readonly IOrderService _orders;

        public OrderShipmentDeliveredHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task HandleAsync(ShipmentDelivered message, CancellationToken ct = default)
        {
            await _orders.MarkAsDeliveredAsync(message.OrderId, ct);
        }
    }
}
