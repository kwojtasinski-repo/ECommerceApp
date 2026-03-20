using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class GiftProductAsyncEvaluator : IAsyncCouponRuleEvaluator
    {
        private readonly IStockAvailabilityChecker _stockChecker;

        public GiftProductAsyncEvaluator(IStockAvailabilityChecker stockChecker)
        {
            _stockChecker = stockChecker;
        }

        public string RuleName => CouponRuleNames.GiftProduct;

        public async Task<CouponRuleEvaluationResult> EvaluateAsync(
            CouponEvaluationContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (!parameters.TryGetValue("productId", out var pidRaw) || !int.TryParse(pidRaw, out var productId))
                return CouponRuleEvaluationResult.Fail("Missing or invalid 'productId' parameter.");

            parameters.TryGetValue("quantity", out var qtyRaw);
            var quantity = int.TryParse(qtyRaw, out var q) && q > 0 ? q : 1;

            var inStock = await _stockChecker.IsInStockAsync(productId, quantity, ct);
            return inStock
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"Gift product {productId} is out of stock.");
        }
    }
}
