namespace ECommerceApp.Application.Sales.Coupons.Results
{
    public enum CouponApplyResult
    {
        Applied,
        CouponNotFound,
        CouponAlreadyUsed,
        OrderAlreadyHasCoupon,
        OrderNotFound
    }
}
