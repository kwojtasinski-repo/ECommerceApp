using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons.ValueObjects
{
    public sealed record CouponCode
    {
        public string Value { get; }

        public CouponCode(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new DomainException("Coupon code is required.");
            }

            if (trimmed.Length > 50)
            {
                throw new DomainException("Coupon code must not exceed 50 characters.");
            }

            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
