using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class OrderTotalScopeEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.OrderTotal;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            // order-total scope: no filtering — applies to the entire order
            return CouponRuleEvaluationResult.Pass();
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            // No parameters required for order-total scope
            return System.Array.Empty<string>();
        }
    }
}
