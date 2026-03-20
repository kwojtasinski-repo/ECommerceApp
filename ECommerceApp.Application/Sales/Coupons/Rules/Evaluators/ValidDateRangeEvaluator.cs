using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ECommerceApp.Application.Sales.Coupons.Rules.Evaluators
{
    public sealed class ValidDateRangeEvaluator : ICouponRuleEvaluator, ICouponRuleParameterValidator
    {
        public string RuleName => CouponRuleNames.ValidDateRange;

        public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
        {
            var now = DateTime.UtcNow;

            if (parameters.TryGetValue("validFrom", out var fromRaw) &&
                DateTime.TryParse(fromRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var validFrom) &&
                now < validFrom)
            {
                return CouponRuleEvaluationResult.Fail($"Coupon is not valid until {validFrom:yyyy-MM-dd}.");
            }

            if (parameters.TryGetValue("validTo", out var toRaw) &&
                DateTime.TryParse(toRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var validTo) &&
                now > validTo)
            {
                return CouponRuleEvaluationResult.Fail($"Coupon expired on {validTo:yyyy-MM-dd}.");
            }

            return CouponRuleEvaluationResult.Pass();
        }

        public IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters)
        {
            var errors = new List<string>();

            if (parameters.TryGetValue("validFrom", out var fromRaw) &&
                !DateTime.TryParse(fromRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("'validFrom' must be a valid date string.");

            if (parameters.TryGetValue("validTo", out var toRaw) &&
                !DateTime.TryParse(toRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("'validTo' must be a valid date string.");

            if (parameters.TryGetValue("validFrom", out var f) && parameters.TryGetValue("validTo", out var t) &&
                DateTime.TryParse(f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) &&
                DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) &&
                to <= from)
            {
                errors.Add("'validTo' must be after 'validFrom'.");
            }

            return errors;
        }
    }
}
