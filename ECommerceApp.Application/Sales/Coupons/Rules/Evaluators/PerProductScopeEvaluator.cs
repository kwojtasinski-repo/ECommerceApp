using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class PerProductScopeEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.PerProduct;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("productId", out var raw) || !int.TryParse(raw, out var productId))
                return CouponRuleEvaluationResult.Fail("Missing or invalid 'productId' parameter.");

            var hasMatchingItems = context.Items.Any(i => i.ProductId == productId);
            return hasMatchingItems
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"No items in the order match product {productId}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("productId", out var raw) || !int.TryParse(raw, out var id) || id <= 0)
                errors.Add("'productId' is required and must be a positive integer.");
            return errors;
        }
    }
}
