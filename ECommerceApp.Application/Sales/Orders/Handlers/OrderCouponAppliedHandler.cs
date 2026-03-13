using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Orders.Services;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderCouponAppliedHandler : IMessageHandler<CouponApplied>
    {
        private readonly IOrderService _orders;

        public OrderCouponAppliedHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task HandleAsync(CouponApplied message, CancellationToken ct = default)
            => await _orders.AddCouponAsync(message.OrderId, message.CouponUsedId, message.DiscountPercent, ct);
    }
}
