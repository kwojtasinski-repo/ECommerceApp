namespace ECommerceApp.Domain.Sales.Orders.Events.Payloads
{
    public record CouponAppliedPayload(int CouponUsedId, int DiscountPercent);
}
