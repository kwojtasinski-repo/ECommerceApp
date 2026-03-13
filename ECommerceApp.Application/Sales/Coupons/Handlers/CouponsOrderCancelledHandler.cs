using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Coupons;

namespace ECommerceApp.Application.Sales.Coupons.Handlers
{
    internal sealed class CouponsOrderCancelledHandler : IMessageHandler<OrderCancelled>
    {
        private readonly ICouponUsedRepository _couponUsed;
        private readonly ICouponRepository _coupons;

        public CouponsOrderCancelledHandler(ICouponUsedRepository couponUsed, ICouponRepository coupons)
        {
            _couponUsed = couponUsed;
            _coupons = coupons;
        }

        public async Task HandleAsync(OrderCancelled message, CancellationToken ct = default)
        {
            var couponUsed = await _couponUsed.FindByOrderIdAsync(message.OrderId, ct);
            if (couponUsed is null)
                return;

            var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
            coupon.Release();
            await _coupons.UpdateAsync(coupon, ct);
            await _couponUsed.DeleteAsync(couponUsed, ct);
        }
    }
}
