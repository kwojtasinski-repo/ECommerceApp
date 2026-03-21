using ECommerceApp.Application.Sales.Coupons.Rules.Evaluators;
using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public sealed class CouponWorkflowBuilder
    {
        private readonly List<CouponRuleDescriptor> _rules = new();

        public CouponWorkflowBuilder DefineRule(
            string name,
            CouponRuleCategory category,
            ICouponRuleEvaluator syncEvaluator,
            IAsyncCouponRuleEvaluator asyncEvaluator = null,
            ICouponRuleParameterValidator parameterValidator = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Rule name is required.", nameof(name));

            _rules.Add(new CouponRuleDescriptor(name, category, syncEvaluator, asyncEvaluator, parameterValidator));
            return this;
        }

        public ICouponRuleRegistry Build()
        {
            var rules = new List<CouponRuleDescriptor>(_rules);

            if (rules.Any(r => r.Name == CouponRuleNames.FixedAmountOff)
                && !rules.Any(r => r.Name == CouponRuleNames.OversizeGuard))
            {
                var evaluator = new CouponOversizeGuardEvaluator();
                rules.Add(new CouponRuleDescriptor(
                    CouponRuleNames.OversizeGuard,
                    CouponRuleCategory.Constraint,
                    evaluator,
                    null,
                    null));
            }

            return new CouponRuleRegistry(rules);
        }

        private sealed class CouponRuleRegistry : ICouponRuleRegistry
        {
            private readonly Dictionary<string, CouponRuleDescriptor> _rulesByName;
            private readonly IReadOnlyList<CouponRuleDescriptor> _allRules;

            public CouponRuleRegistry(IEnumerable<CouponRuleDescriptor> rules)
            {
                _allRules = rules.ToList();
                _rulesByName = _allRules.ToDictionary(r => r.Name);
            }

            public CouponRuleDescriptor GetRule(string ruleName)
            {
                if (_rulesByName.TryGetValue(ruleName, out var descriptor))
                    return descriptor;

                throw new KeyNotFoundException($"Rule '{ruleName}' is not registered.");
            }

            public bool TryGetRule(string ruleName, out CouponRuleDescriptor descriptor)
            {
                return _rulesByName.TryGetValue(ruleName, out descriptor);
            }

            public IReadOnlyList<CouponRuleDescriptor> GetAllRules() => _allRules;

            public IReadOnlyList<CouponRuleDescriptor> GetRulesByCategory(CouponRuleCategory category)
            {
                return _allRules.Where(r => r.Category == category).ToList();
            }
        }
    }
}
