using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class PercentageOffEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.PercentageOff;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("percent", out var raw))
                raw = "10"; // defaults-when-missing convention

            if (!decimal.TryParse(raw, out var percent) || percent <= 0 || percent > 100)
                return CouponRuleEvaluationResult.Fail("Invalid 'percent' parameter.");

            var reduction = context.OriginalTotal * percent / 100m;
            return CouponRuleEvaluationResult.Pass(reduction);
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("percent", out var raw))
            {
                if (!decimal.TryParse(raw, out var p) || p <= 0 || p > 100)
                    errors.Add("'percent' must be a decimal between 0 (exclusive) and 100 (inclusive).");
            }
            return errors;
        }
    }
}
