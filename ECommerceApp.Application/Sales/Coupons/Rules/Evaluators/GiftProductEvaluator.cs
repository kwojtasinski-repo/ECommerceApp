using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class GiftProductEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        // Note: Tier 1 (sync) checks the cart for the gift product presence.
        // Tier 2 (async) would perform the stock check — see GiftProductAsyncEvaluator.
        public string RuleName => CouponRuleNames.GiftProduct;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("productId", out var pidRaw) || !int.TryParse(pidRaw, out var productId))
                return CouponRuleEvaluationResult.Fail("Missing or invalid 'productId' parameter.");

            parameters.TryGetValue("quantity", out var qtyRaw);
            var quantity = int.TryParse(qtyRaw, out var q) && q > 0 ? q : 1;

            // Gift product adds a free product to the order.
            // The item may or may not already be in the cart; the reduction equals its unit price × quantity.
            var matchingItem = context.Items.FirstOrDefault(i => i.ProductId == productId);
            if (matchingItem == null)
            {
                // Gift product not yet in cart — Tier 1 passes; stock check deferred to Tier 2.
                return CouponRuleEvaluationResult.Pass();
            }

            var freeUnits = System.Math.Min(quantity, matchingItem.Quantity);
            var reduction = matchingItem.UnitPrice * freeUnits;
            return CouponRuleEvaluationResult.Pass(reduction);
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("productId", out var raw) || !int.TryParse(raw, out var id) || id <= 0)
                errors.Add("'productId' is required and must be a positive integer.");
            if (parameters.TryGetValue("quantity", out var qtyRaw) && (!int.TryParse(qtyRaw, out var qty) || qty <= 0))
                errors.Add("'quantity' must be a positive integer when provided.");
            return errors;
        }
    }
}
