namespace ECommerceApp.Application.Sales.Coupons
{
    public sealed class CouponsOptions
    {
        public int MaxCouponsPerOrder { get; set; } = 5;        // hard ceiling: 10
        public decimal DefaultMinOrderValue { get; set; } = 100m;
    }
}
