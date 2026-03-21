using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class CouponOversizeGuardEvaluator : ICouponRuleEvaluator
    {
        public string RuleName => CouponRuleNames.OversizeGuard;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (context.BypassOversizeGuard)
            {
                return CouponRuleEvaluationResult.Pass();
            }

            if (!parameters.TryGetValue("amount", out var raw))
            {
                return CouponRuleEvaluationResult.Pass();
            }

            if (!decimal.TryParse(raw, out var amount) || amount <= 0)
            {
                return CouponRuleEvaluationResult.Pass();
            }

            if (amount > context.OriginalTotal)
            {
                return CouponRuleEvaluationResult.Fail(
                    $"Fixed-amount discount ({amount:F2}) exceeds order total ({context.OriginalTotal:F2}).");
            }

            return CouponRuleEvaluationResult.Pass();
        }
    }
}
