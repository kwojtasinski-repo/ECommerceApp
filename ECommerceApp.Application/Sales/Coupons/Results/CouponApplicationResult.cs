namespace ECommerceApp.Application.Sales.Coupons.Results
{
    public sealed class CouponApplicationResult
    {
        public bool Success { get; }
        public decimal Reduction { get; }
        public string FailureReason { get; }
        public bool IsExclusive { get; }

        private CouponApplicationResult(bool success, decimal reduction, string failureReason, bool isExclusive)
        {
            Success = success;
            Reduction = reduction;
            FailureReason = failureReason;
            IsExclusive = isExclusive;
        }

        public static CouponApplicationResult Applied(decimal reduction, bool isExclusive = false)
            => new(true, reduction, string.Empty, isExclusive);

        public static CouponApplicationResult Failed(string reason)
            => new(false, 0m, reason, false);
    }
}
