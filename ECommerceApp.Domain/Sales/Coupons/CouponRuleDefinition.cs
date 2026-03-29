using System.Collections.Generic;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public enum CouponRuleCategory { Scope, Discount, Constraint }

    public sealed record CouponRuleDefinition(
        string Name,
        CouponRuleCategory Category,
        IReadOnlyDictionary<string, string> Parameters);
}
