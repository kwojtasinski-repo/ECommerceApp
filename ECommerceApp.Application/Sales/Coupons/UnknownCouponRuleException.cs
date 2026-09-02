using System;

namespace ECommerceApp.Application.Sales.Coupons
{
    /// <summary>
    /// Raised when a submitted coupon rule name is not in the registry — i.e. the client sent a rule
    /// the builder never offered. Distinct from <see cref="InvalidOperationException"/> on purpose:
    /// the create path must be able to reject bad input without also swallowing the
    /// <see cref="InvalidOperationException"/>s that EF raises for genuine persistence failures.
    /// </summary>
    public sealed class UnknownCouponRuleException : Exception
    {
        public string RuleName { get; }

        public UnknownCouponRuleException(string ruleName)
            : base($"Unknown coupon rule '{ruleName}'.")
        {
            RuleName = ruleName;
        }
    }
}
