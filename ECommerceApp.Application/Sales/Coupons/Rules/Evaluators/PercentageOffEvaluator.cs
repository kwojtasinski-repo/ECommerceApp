using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Globalization;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class PercentageOffEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator, ICouponRuleParameterSchema
    {
        private readonly CouponsOptions _options;

        public PercentageOffEvaluator(CouponsOptions options)
        {
            _options = options;
        }

        public string RuleName => CouponRuleNames.PercentageOff;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("percent", out var raw))
            {
                raw = _options.DefaultDiscountPercent.ToString(CultureInfo.InvariantCulture);
            }

            if (!decimal.TryParse(raw, out var percent) || percent <= 0 || percent > 100)
                return CouponRuleEvaluationResult.Fail("Invalid 'percent' parameter.");

            var reduction = context.OriginalTotal * percent / 100m;
            return CouponRuleEvaluationResult.Pass(reduction);
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("percent", out var raw))
            {
                if (!decimal.TryParse(raw, out var p) || p <= 0 || p > 100)
                    errors.Add("'percent' must be a decimal between 0 (exclusive) and 100 (inclusive).");
            }
            return errors;
        }

        public IReadOnlyList<CouponRuleParameter> GetParameters()
        {
            return new[]
            {
                new CouponRuleParameter("percent", CouponRuleParameterType.Decimal, false, _options.DefaultDiscountPercent.ToString(CultureInfo.InvariantCulture), 0m, 100m, "Percentage off")
            };
        }
    }
}
