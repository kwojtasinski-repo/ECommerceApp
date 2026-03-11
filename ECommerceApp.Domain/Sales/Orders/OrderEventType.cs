namespace ECommerceApp.Domain.Sales.Orders
{
    public enum OrderEventType
    {
        OrderPlaced,
        OrderPaid,
        OrderDelivered,
        CouponApplied,
        CouponRemoved,
        RefundAssigned,
        RefundRemoved,
        OrderCancelled
    }
}
