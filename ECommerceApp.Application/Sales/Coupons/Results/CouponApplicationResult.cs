namespace ECommerceApp.Application.Sales.Coupons.Results
{
    public sealed class CouponApplicationResult
    {
        public bool Success { get; }
        public string FailureReason { get; }

        private CouponApplicationResult(bool success, string failureReason)
        {
            Success = success;
            FailureReason = failureReason;
        }

        public static CouponApplicationResult Applied()
            => new(true, string.Empty);

        public static CouponApplicationResult Failed(string reason)
            => new(false, reason);
    }
}
