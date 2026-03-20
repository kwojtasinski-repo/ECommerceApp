using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class FreeCheapestItemEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.FreeCheapestItem;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (context.Items.Count == 0)
                return CouponRuleEvaluationResult.Fail("Cart is empty.");

            parameters.TryGetValue("maxFreeUnits", out var raw);
            var maxFreeUnits = int.TryParse(raw, out var m) && m > 0 ? m : 1;

            // Auto-select cheapest item(s) from cart ordered by unit price ascending
            var sortedItems = context.Items.OrderBy(i => i.UnitPrice).ToList();

            var remaining = maxFreeUnits;
            var reduction = 0m;

            foreach (var item in sortedItems)
            {
                if (remaining <= 0)
                    break;

                var freeUnits = Math.Min(remaining, item.Quantity);
                reduction += item.UnitPrice * freeUnits;
                remaining -= freeUnits;
            }

            return CouponRuleEvaluationResult.Pass(reduction);
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("maxFreeUnits", out var raw) && (!int.TryParse(raw, out var m) || m <= 0))
                errors.Add("'maxFreeUnits' must be a positive integer when provided.");
            return errors;
        }
    }
}
