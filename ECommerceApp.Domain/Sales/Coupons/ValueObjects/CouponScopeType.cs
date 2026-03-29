using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons.ValueObjects
{
    public sealed record CouponScopeType
    {
        public string Value { get; }

        public CouponScopeType(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new DomainException("ScopeType is required.");
            }

            if (trimmed != CouponRuleNames.PerProduct &&
                trimmed != CouponRuleNames.PerCategory &&
                trimmed != CouponRuleNames.PerTag)
            {
                throw new DomainException($"Invalid scope type '{trimmed}'. Allowed: per-product, per-category, per-tag.");
            }

            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
