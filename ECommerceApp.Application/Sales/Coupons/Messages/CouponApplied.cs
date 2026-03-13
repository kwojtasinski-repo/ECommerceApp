using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Sales.Coupons.Messages
{
    public record CouponApplied(int OrderId, int CouponUsedId, int DiscountPercent) : IMessage;
}
