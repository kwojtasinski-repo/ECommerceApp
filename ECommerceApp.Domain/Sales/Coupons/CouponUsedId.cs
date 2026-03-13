using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public sealed record CouponUsedId(int Value) : TypedId<int>(Value);
}
