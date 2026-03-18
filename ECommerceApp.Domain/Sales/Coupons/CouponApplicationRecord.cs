using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record CouponApplicationRecordId(int Value) : TypedId<int>(Value);

    public class CouponApplicationRecord
    {
        public CouponApplicationRecordId Id { get; private set; }
        public int CouponUsedId { get; private set; }       // plain int — no DB FK; audit ref only
        public string CouponCode { get; private set; }
        public string DiscountType { get; private set; }
        public decimal DiscountValue { get; private set; }
        public decimal OriginalTotal { get; private set; }
        public decimal Reduction { get; private set; }
        public DateTime AppliedAt { get; private set; }
        public bool WasReversed { get; private set; }
        public DateTime? ReversedAt { get; private set; }

        private CouponApplicationRecord() { }

        public static CouponApplicationRecord Create(
            int couponUsedId, string couponCode, string discountType,
            decimal discountValue, decimal originalTotal, decimal reduction)
        {
            if (couponUsedId <= 0)
                throw new DomainException("CouponUsedId must be positive.");
            if (string.IsNullOrWhiteSpace(couponCode))
                throw new DomainException("CouponCode is required.");
            if (string.IsNullOrWhiteSpace(discountType))
                throw new DomainException("DiscountType is required.");
            if (reduction < 0)
                throw new DomainException("Reduction cannot be negative.");

            return new CouponApplicationRecord
            {
                CouponUsedId = couponUsedId,
                CouponCode = couponCode,
                DiscountType = discountType,
                DiscountValue = discountValue,
                OriginalTotal = originalTotal,
                Reduction = reduction,
                AppliedAt = DateTime.UtcNow,
                WasReversed = false
            };
        }

        public void MarkAsReversed()
        {
            if (WasReversed)
                throw new DomainException("CouponApplicationRecord is already reversed.");
            WasReversed = true;
            ReversedAt = DateTime.UtcNow;
        }
    }
}
