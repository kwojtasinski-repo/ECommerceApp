using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class MaxUsesPerUserEvaluator : IAsyncCouponRuleEvaluator, ICouponRuleParameterValidator
    {
        private readonly ICouponUsedRepository _couponUsedRepo;

        public MaxUsesPerUserEvaluator(ICouponUsedRepository couponUsedRepo)
        {
            _couponUsedRepo = couponUsedRepo;
        }

        public string RuleName => CouponRuleNames.MaxUsesPerUser;

        public async Task<CouponRuleEvaluationResult> EvaluateAsync(
            CouponEvaluationContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            parameters.TryGetValue("maxUsesPerUser", out var raw);
            var maxUsesPerUser = int.TryParse(raw, out var m) && m > 0 ? m : 1;

            // Count usages for this specific user
            var userUsageCount = await _couponUsedRepo.CountByUserAndCouponAsync(context.UserId, context.OrderId, ct);

            return userUsageCount < maxUsesPerUser
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"User has reached the per-user usage limit of {maxUsesPerUser}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("maxUsesPerUser", out var raw) && (!int.TryParse(raw, out var v) || v <= 0))
                errors.Add("'maxUsesPerUser' must be a positive integer when provided.");
            return errors;
        }
    }
}
