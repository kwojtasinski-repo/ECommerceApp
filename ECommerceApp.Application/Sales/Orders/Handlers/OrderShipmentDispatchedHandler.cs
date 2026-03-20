using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentDispatchedHandler : IMessageHandler<ShipmentDispatched>
    {
        private readonly IOrderRepository _orderRepo;

        public OrderShipmentDispatchedHandler(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        public async Task HandleAsync(ShipmentDispatched message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(message.OrderId, ct);
            if (order is null)
            {
                return;
            }

            order.RecordShipmentDispatched();
            await _orderRepo.UpdateAsync(order, ct);
        }
    }
}
