using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Sales.Coupons.Messages
{
    public record CouponRemovedFromOrder(int OrderId) : IMessage;
}
