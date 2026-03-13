using System;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public class CouponUsed
    {
        public CouponUsedId Id { get; private set; }
        public CouponId CouponId { get; private set; }
        public int OrderId { get; private set; }
        public DateTime UsedAt { get; private set; }

        private CouponUsed() { }

        public static CouponUsed Create(CouponId couponId, int orderId)
            => new CouponUsed
            {
                CouponId = couponId,
                OrderId = orderId,
                UsedAt = DateTime.UtcNow
            };
    }
}
