using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record CouponId(int Value) : TypedId<int>(Value);
}
