using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Globalization;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class MinOrderValueEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator, ICouponRuleParameterSchema
    {
        private readonly CouponsOptions _options;

        public MinOrderValueEvaluator(CouponsOptions options)
        {
            _options = options;
        }

        public string RuleName => CouponRuleNames.MinOrderValue;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            parameters.TryGetValue("minValue", out var raw);
            var minValue = decimal.TryParse(raw, out var v) ? v : _options.DefaultMinOrderValue;

            return context.OriginalTotal >= minValue
                ? CouponRuleEvaluationResult.Pass()
                : CouponRuleEvaluationResult.Fail($"Order total {context.OriginalTotal:F2} is below the minimum {minValue:F2}.");
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();
            if (parameters.TryGetValue("minValue", out var raw) && (!decimal.TryParse(raw, out var v) || v < 0))
                errors.Add("'minValue' must be a non-negative decimal when provided.");
            return errors;
        }

        public IReadOnlyList<CouponRuleParameter> GetParameters()
        {
            return new[]
            {
                new CouponRuleParameter("minValue", CouponRuleParameterType.Decimal, false, _options.DefaultMinOrderValue.ToString(CultureInfo.InvariantCulture), 0m, null, "Minimum order value")
            };
        }
    }
}
