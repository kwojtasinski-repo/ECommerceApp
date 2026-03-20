using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class FirstPurchaseOnlyEvaluator : IAsyncCouponRuleEvaluator, ICouponRuleParameterValidator
    {
        private readonly ICompletedOrderCounter _completedOrderCounter;

        public FirstPurchaseOnlyEvaluator(ICompletedOrderCounter completedOrderCounter)
        {
            _completedOrderCounter = completedOrderCounter;
        }

        public string RuleName => CouponRuleNames.FirstPurchaseOnly;

        public async Task<CouponRuleEvaluationResult> EvaluateAsync(
            CouponEvaluationContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            var completedOrderCount = await _completedOrderCounter.CountByUserAsync(context.UserId, ct);

            return completedOrderCount == 0
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail("Coupon is only valid for first-time purchases.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            // No parameters required — uses zero-parameter convention
            return System.Array.Empty<string>();
        }
    }
}
