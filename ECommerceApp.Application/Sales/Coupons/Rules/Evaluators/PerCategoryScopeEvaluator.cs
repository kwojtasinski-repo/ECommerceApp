using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class PerCategoryScopeEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.PerCategory;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("categoryId", out var raw) || !int.TryParse(raw, out var categoryId))
                return CouponRuleEvaluationResult.Fail("Missing or invalid 'categoryId' parameter.");

            var hasMatchingItems = context.Items.Any(i => i.CategoryId == categoryId);
            return hasMatchingItems
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"No items in the order match category {categoryId}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("categoryId", out var raw) || !int.TryParse(raw, out var id) || id <= 0)
                errors.Add("'categoryId' is required and must be a positive integer.");
            return errors;
        }
    }
}
