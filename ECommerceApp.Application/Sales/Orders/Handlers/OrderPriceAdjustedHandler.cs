using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderPriceAdjustedHandler : IMessageHandler<OrderPriceAdjusted>
    {
        private readonly IOrderRepository _orderRepo;

        public OrderPriceAdjustedHandler(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        public async Task HandleAsync(OrderPriceAdjusted message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
            if (order is null)
            {
                return;
            }

            order.AdjustPrice(message.NewPrice);
            await _orderRepo.UpdateAsync(order, ct);
        }
    }
}
