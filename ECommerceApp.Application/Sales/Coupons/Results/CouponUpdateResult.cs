namespace ECommerceApp.Application.Sales.Coupons.Results
{
    public enum CouponUpdateOutcome
    {
        Updated,
        NotFound,
        ValidationFailed,
        ConcurrencyConflict
    }

    public sealed class CouponUpdateResult
    {
        private CouponUpdateResult(CouponUpdateOutcome outcome, string failureReason = null)
        {
            Outcome = outcome;
            FailureReason = failureReason ?? string.Empty;
        }

        public CouponUpdateOutcome Outcome { get; }
        public string FailureReason { get; }

        public static CouponUpdateResult Updated()
            => new(CouponUpdateOutcome.Updated);

        public static CouponUpdateResult NotFound()
            => new(CouponUpdateOutcome.NotFound);

        public static CouponUpdateResult ValidationFailed(string reason)
            => new(CouponUpdateOutcome.ValidationFailed, reason);

        public static CouponUpdateResult ConcurrencyConflict()
            => new(CouponUpdateOutcome.ConcurrencyConflict);
    }
}