using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public class CouponUsed
    {
        public CouponUsedId Id { get; private set; }
        public CouponId CouponId { get; private set; }             // nullable in Slice 2 — set for DB coupons
        public int OrderId { get; private set; }
        public DateTime UsedAt { get; private set; }

        // ── Slice 2 additions ────────────────────────────────────────────
        public string UserId { get; private set; }                  // required in Slice 2
        public string RuntimeCouponSnapshot { get; private set; }   // JSON — set for runtime/ML coupons

        private CouponUsed() { }

        // Slice 1 factory — kept for backward compatibility
        public static CouponUsed Create(CouponId couponId, int orderId)
            => new CouponUsed
            {
                CouponId = couponId,
                OrderId = orderId,
                UsedAt = DateTime.UtcNow
            };

        // Slice 2 factory — DB coupon usage
        public static CouponUsed CreateForDbCoupon(CouponId couponId, int orderId, string userId)
        {
            if (couponId is null)
                throw new DomainException("CouponId is required for DB coupon usage.");
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("UserId is required.");

            return new CouponUsed
            {
                CouponId = couponId,
                OrderId = orderId,
                UserId = userId,
                UsedAt = DateTime.UtcNow
            };
        }

        // Slice 2 factory — runtime/ML coupon usage
        public static CouponUsed CreateForRuntimeCoupon(string runtimeCouponSnapshot, int orderId, string userId)
        {
            if (string.IsNullOrWhiteSpace(runtimeCouponSnapshot))
                throw new DomainException("RuntimeCouponSnapshot JSON is required for runtime coupon usage.");
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("UserId is required.");

            return new CouponUsed
            {
                RuntimeCouponSnapshot = runtimeCouponSnapshot,
                OrderId = orderId,
                UserId = userId,
                UsedAt = DateTime.UtcNow
            };
        }
    }
}
