using ECommerceApp.Domain.Sales.Coupons;
using System;
using System.Collections.Generic;

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
            throw new NotImplementedException("Rule registry construction — Slice 2");
        }
    }
}
