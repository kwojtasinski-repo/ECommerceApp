using ECommerceApp.Domain.Sales.Coupons;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public sealed class CouponRulePipelineResult
    {
        public bool Passed { get; }
        public decimal TotalReduction { get; }
        public IReadOnlyList<string> FailureReasons { get; }

        private CouponRulePipelineResult(bool passed, decimal totalReduction, IReadOnlyList<string> failureReasons)
        {
            Passed = passed;
            TotalReduction = totalReduction;
            FailureReasons = failureReasons;
        }

        public static CouponRulePipelineResult Success(decimal totalReduction)
            => new(true, totalReduction, System.Array.Empty<string>());

        public static CouponRulePipelineResult Failure(IReadOnlyList<string> reasons)
            => new(false, 0m, reasons);
    }

    public interface ICouponRulePipeline
    {
        Task<CouponRulePipelineResult> EvaluateAsync(
            IReadOnlyList<CouponRuleDefinition> rules,
            CouponEvaluationContext context,
            CancellationToken ct = default);
    }

    public sealed class CouponRulePipeline : ICouponRulePipeline
    {
        private readonly ICouponRuleRegistry _registry;

        public CouponRulePipeline(ICouponRuleRegistry registry)
        {
            _registry = registry;
        }

        public async Task<CouponRulePipelineResult> EvaluateAsync(
            IReadOnlyList<CouponRuleDefinition> rules,
            CouponEvaluationContext context,
            CancellationToken ct = default)
        {
            // ── Tier 1: sync evaluation (zero DB) ────────────────────────
            var totalReduction = 0m;
            var tier1Failures = new List<string>();

            foreach (var rule in rules)
            {
                if (!_registry.TryGetRule(rule.Name, out var descriptor))
                {
                    tier1Failures.Add($"Unknown rule '{rule.Name}'.");
                    continue;
                }

                if (descriptor.SyncEvaluator != null)
                {
                    var parameters = (IReadOnlyDictionary<string, string>)rule.Parameters
                                     ?? new Dictionary<string, string>();
                    var result = descriptor.SyncEvaluator.Evaluate(context, parameters);

                    if (!result.Passed)
                    {
                        tier1Failures.Add(result.FailureReason);
                    }
                    else
                    {
                        totalReduction += result.Reduction;
                    }
                }
            }

            // If any Tier 1 rule failed, Tier 2 is skipped
            if (tier1Failures.Count > 0)
                return CouponRulePipelineResult.Failure(tier1Failures);

            // ── Tier 2: async evaluation (DB/cache) ──────────────────────
            var tier2Failures = new List<string>();

            foreach (var rule in rules)
            {
                if (!_registry.TryGetRule(rule.Name, out var descriptor))
                    continue; // already reported in Tier 1

                if (descriptor.AsyncEvaluator != null)
                {
                    var parameters = (IReadOnlyDictionary<string, string>)rule.Parameters
                                     ?? new Dictionary<string, string>();
                    var result = await descriptor.AsyncEvaluator.EvaluateAsync(context, parameters, ct);

                    if (!result.Passed)
                    {
                        tier2Failures.Add(result.FailureReason);
                    }
                    else
                    {
                        totalReduction += result.Reduction;
                    }
                }
            }

            if (tier2Failures.Count > 0)
                return CouponRulePipelineResult.Failure(tier2Failures);

            return CouponRulePipelineResult.Success(totalReduction);
        }
    }
}
