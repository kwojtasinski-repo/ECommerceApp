using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class FixedAmountOffEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.FixedAmountOff;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("amount", out var raw))
                raw = "0";

            if (!decimal.TryParse(raw, out var amount) || amount <= 0)
                return CouponRuleEvaluationResult.Fail("Invalid 'amount' parameter.");

            // Cap reduction at originalTotal (floor at 0)
            var reduction = Math.Min(amount, context.OriginalTotal);
            return CouponRuleEvaluationResult.Pass(reduction);
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("amount", out var raw) || !decimal.TryParse(raw, out var a) || a <= 0)
                errors.Add("'amount' is required and must be a positive decimal.");
            return errors;
        }
    }
}
