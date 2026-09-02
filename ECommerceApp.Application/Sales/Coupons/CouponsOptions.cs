namespace ECommerceApp.Application.Sales.Coupons
{
    public sealed class CouponsOptions
    {
        public const string SectionName = "Coupons";

        public int MaxCouponsPerOrder { get; set; } = 5;        // hard ceiling: 10

        /// <summary>
        /// Fallback used by the <c>min-order-value</c> rule when the coupon does not set one. Also
        /// the default shown for that parameter in the admin rule builder — both read this property,
        /// so the two cannot drift apart.
        /// </summary>
        public decimal DefaultMinOrderValue { get; set; } = 100m;

        /// <summary>
        /// Fallback used by the <c>percentage-off</c> rule when the coupon does not set one, and the
        /// default shown for that parameter in the admin rule builder.
        /// </summary>
        public decimal DefaultDiscountPercent { get; set; } = 10m;
    }
}
