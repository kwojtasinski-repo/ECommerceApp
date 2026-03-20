using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentPartiallyDeliveredHandler : IMessageHandler<ShipmentPartiallyDelivered>
    {
        private readonly IOrderRepository _orderRepo;

        public OrderShipmentPartiallyDeliveredHandler(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
            if (order is null)
            {
                return;
            }

            order.MarkAsPartiallyFulfilled();
            await _orderRepo.UpdateAsync(order, ct);
        }
    }
}
