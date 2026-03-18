namespace ECommerceApp.Application.Sales.Coupons.Rules
{
    public sealed class CouponRuleEvaluationResult
    {
        public bool Passed { get; }
        public string FailureReason { get; }
        public decimal Reduction { get; }

        private CouponRuleEvaluationResult(bool passed, string failureReason, decimal reduction)
        {
            Passed = passed;
            FailureReason = failureReason;
            Reduction = reduction;
        }

        public static CouponRuleEvaluationResult Pass(decimal reduction = 0m)
            => new(true, string.Empty, reduction);

        public static CouponRuleEvaluationResult Fail(string reason)
            => new(false, reason, 0m);
    }
}
