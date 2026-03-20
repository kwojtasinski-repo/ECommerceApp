using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class MinOrderValueEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.MinOrderValue;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            // defaults-when-missing convention — fallback to 100 (from CouponsOptions.DefaultMinOrderValue)
            parameters.TryGetValue("minValue", out var raw);
            var minValue = decimal.TryParse(raw, out var v) ? v : 100m;

            return context.OriginalTotal >= minValue
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"Order total {context.OriginalTotal:F2} is below the minimum {minValue:F2}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("minValue", out var raw) && (!decimal.TryParse(raw, out var v) || v < 0))
                errors.Add("'minValue' must be a non-negative decimal when provided.");
            return errors;
        }
    }
}
