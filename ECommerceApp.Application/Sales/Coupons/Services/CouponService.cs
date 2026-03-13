using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Domain.Sales.Coupons;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    internal sealed class CouponService : ICouponService
    {
        private readonly ICouponRepository _coupons;
        private readonly ICouponUsedRepository _couponUsed;
        private readonly IOrderExistenceChecker _orderExistence;
        private readonly IMessageBroker _broker;

        public CouponService(
            ICouponRepository coupons,
            ICouponUsedRepository couponUsed,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker)
        {
            _coupons = coupons;
            _couponUsed = couponUsed;
            _orderExistence = orderExistence;
            _broker = broker;
        }

        public async Task<CouponApplyResult> ApplyCouponAsync(string couponCode, int orderId, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(orderId, ct))
                return CouponApplyResult.OrderNotFound;

            var coupon = await _coupons.GetByCodeAsync(couponCode, ct);
            if (coupon is null)
                return CouponApplyResult.CouponNotFound;

            if (coupon.Status != CouponStatus.Available)
                return CouponApplyResult.CouponAlreadyUsed;

            var existing = await _couponUsed.FindByOrderIdAsync(orderId, ct);
            if (existing is not null)
                return CouponApplyResult.OrderAlreadyHasCoupon;

            coupon.MarkAsUsed();
            var couponUsed = CouponUsed.Create(coupon.Id, orderId);
            await _couponUsed.AddAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);
            await _broker.PublishAsync(new CouponApplied(orderId, couponUsed.Id.Value, coupon.DiscountPercent));
            return CouponApplyResult.Applied;
        }

        public async Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default)
        {
            var couponUsed = await _couponUsed.FindByOrderIdAsync(orderId, ct);
            if (couponUsed is null)
                return CouponRemoveResult.NoCouponApplied;

            var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
            coupon.Release();
            await _couponUsed.DeleteAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);
            await _broker.PublishAsync(new CouponRemovedFromOrder(orderId));
            return CouponRemoveResult.Removed;
        }
    }
}
