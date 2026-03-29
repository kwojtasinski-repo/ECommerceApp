using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons.ValueObjects
{
    public sealed record CouponDescription
    {
        public string Value { get; }

        public CouponDescription(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new DomainException("Description is required.");
            }

            if (trimmed.Length > 500)
            {
                throw new DomainException("Description must not exceed 500 characters.");
            }

            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
