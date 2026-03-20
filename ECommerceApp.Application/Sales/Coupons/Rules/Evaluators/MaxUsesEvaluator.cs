using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class MaxUsesEvaluator : IAsyncCouponRuleEvaluator, ICouponRuleParameterValidator
    {
        private readonly ICouponUsedRepository _couponUsedRepo;

        public MaxUsesEvaluator(ICouponUsedRepository couponUsedRepo)
        {
            _couponUsedRepo = couponUsedRepo;
        }

        public string RuleName => CouponRuleNames.MaxUses;

        public async Task<CouponRuleEvaluationResult> EvaluateAsync(
            CouponEvaluationContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            parameters.TryGetValue("maxUses", out var raw);
            var maxUses = int.TryParse(raw, out var m) && m > 0 ? m : int.MaxValue;

            // Count all usages for this coupon across all users
            var usages = await _couponUsedRepo.FindAllByOrderIdAsync(context.OrderId, ct);
            var currentUsageCount = usages.Count;

            return currentUsageCount < maxUses
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"Coupon has reached its maximum usage limit of {maxUses}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("maxUses", out var raw) && (!int.TryParse(raw, out var v) || v <= 0))
                errors.Add("'maxUses' must be a positive integer when provided.");
            return errors;
        }
    }
}
