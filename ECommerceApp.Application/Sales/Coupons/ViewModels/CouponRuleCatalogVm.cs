using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.ViewModels
{
    public sealed class CouponRuleCatalogVm
    {
        public IReadOnlyList<CouponRuleCatalogCategoryVm> Categories { get; init; } = new List<CouponRuleCatalogCategoryVm>();
    }

    public sealed class CouponRuleCatalogCategoryVm
    {
        public CouponRuleCategory Category { get; init; }
        public IReadOnlyList<CouponRuleCatalogRuleVm> Rules { get; init; } = new List<CouponRuleCatalogRuleVm>();
    }

    public sealed class CouponRuleCatalogRuleVm
    {
        public string Name { get; init; }
        public bool RequiresScopeTargets { get; init; }
        public IReadOnlyList<CouponRuleParameter> Parameters { get; init; } = new List<CouponRuleParameter>();
    }
}