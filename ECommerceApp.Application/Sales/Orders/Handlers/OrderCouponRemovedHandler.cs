using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Orders.Services;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderCouponRemovedHandler : IMessageHandler<CouponRemovedFromOrder>
    {
        private readonly IOrderService _orders;

        public OrderCouponRemovedHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task HandleAsync(CouponRemovedFromOrder message, CancellationToken ct = default)
            => await _orders.RemoveCouponAsync(message.OrderId, ct);
    }
}
