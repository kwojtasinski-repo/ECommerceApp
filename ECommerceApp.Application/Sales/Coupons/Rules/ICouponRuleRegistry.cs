using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public sealed class CouponRuleDescriptor
    {
        public string Name { get; }
        public CouponRuleCategory Category { get; }
        public ICouponRuleEvaluator SyncEvaluator { get; }
        public IAsyncCouponRuleEvaluator AsyncEvaluator { get; }
        public ICouponRuleParameterValidator ParameterValidator { get; }

        public CouponRuleDescriptor(
            string name,
            CouponRuleCategory category,
            ICouponRuleEvaluator syncEvaluator,
            IAsyncCouponRuleEvaluator asyncEvaluator,
            ICouponRuleParameterValidator parameterValidator)
        {
            Name = name;
            Category = category;
            SyncEvaluator = syncEvaluator;
            AsyncEvaluator = asyncEvaluator;
            ParameterValidator = parameterValidator;
        }
    }

    public interface ICouponRuleRegistry
    {
        CouponRuleDescriptor GetRule(string ruleName);
        bool TryGetRule(string ruleName, out CouponRuleDescriptor descriptor);
        IReadOnlyList<CouponRuleDescriptor> GetAllRules();
        IReadOnlyList<CouponRuleDescriptor> GetRulesByCategory(CouponRuleCategory category);
    }
}
