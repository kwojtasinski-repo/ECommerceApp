using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public interface ICouponRuleEvaluator
    {
        string RuleName { get; }
        CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters);
    }

    public interface IAsyncCouponRuleEvaluator
    {
        string RuleName { get; }
        Task<CouponRuleEvaluationResult> EvaluateAsync(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters, CancellationToken ct = default);
    }

    public interface ICouponRuleParameterValidator
    {
        string RuleName { get; }
        IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string> parameters);
    }
}
