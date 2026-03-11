namespace ECommerceApp.Domain.Sales.Orders
{
    public enum OrderEventType
    {
        OrderPlaced,
        OrderPaymentConfirmed,
        OrderPaymentExpired,
        OrderFulfilled,
        CouponApplied,
        CouponRemoved,
        RefundAssigned,
        RefundRemoved,
        OrderCancelled
    }
}
