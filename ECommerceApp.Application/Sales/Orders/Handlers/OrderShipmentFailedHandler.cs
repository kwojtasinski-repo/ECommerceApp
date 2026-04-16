using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.Events.Payloads;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentFailedHandler : IMessageHandler<ShipmentFailed>
    {
        private readonly IOrderRepository _orderRepo;

        public OrderShipmentFailedHandler(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        public async Task HandleAsync(ShipmentFailed message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdAsync(message.OrderId, ct);
            if (order is null)
            {
                return;
            }

            var failedItems = message.Items
                .Select(i => new FailedShipmentItem(i.ProductId, i.Quantity))
                .ToList();
            order.RecordShipmentFailure(message.ShipmentId, failedItems);
            await _orderRepo.UpdateAsync(order, ct);
        }
    }
}
