using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class SpecialEventEvaluator : IAsyncCouponRuleEvaluator, ICouponRuleParameterValidator
    {
        private readonly ISpecialEventCache _specialEventCache;

        public SpecialEventEvaluator(ISpecialEventCache specialEventCache)
        {
            _specialEventCache = specialEventCache;
        }

        public string RuleName => CouponRuleNames.SpecialEvent;

        public async Task<CouponRuleEvaluationResult> EvaluateAsync(
            CouponEvaluationContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (!parameters.TryGetValue("eventCode", out var eventCode) || string.IsNullOrWhiteSpace(eventCode))
                return CouponRuleEvaluationResult.Fail("Missing 'eventCode' parameter.");

            var specialEvent = await _specialEventCache.GetByCodeAsync(eventCode, ct);
            if (specialEvent == null)
                return CouponRuleEvaluationResult.Fail($"Special event '{eventCode}' not found.");

            return specialEvent.IsCurrentlyActive(DateTime.UtcNow)
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"Special event '{eventCode}' is not currently active.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (!parameters.TryGetValue("eventCode", out var code) || string.IsNullOrWhiteSpace(code))
                errors.Add("'eventCode' is required.");
            return errors;
        }
    }
}
