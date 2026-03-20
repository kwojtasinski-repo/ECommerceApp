using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class PerTagScopeEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.PerTag;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("tagId", out var raw) || !int.TryParse(raw, out var tagId))
                return CouponRuleEvaluationResult.Fail("Missing or invalid 'tagId' parameter.");

            var hasMatchingItems = context.Items.Any(i => i.TagIds.Contains(tagId));
            return hasMatchingItems
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"No items in the order match tag {tagId}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("tagId", out var raw) || !int.TryParse(raw, out var id) || id <= 0)
                errors.Add("'tagId' is required and must be a positive integer.");
            return errors;
        }
    }
}
