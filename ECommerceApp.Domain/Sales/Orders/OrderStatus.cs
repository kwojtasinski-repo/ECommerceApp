namespace ECommerceApp.Domain.Sales.Orders
{
    public enum OrderStatus
    {
        Placed,
        PaymentConfirmed,
        PartiallyFulfilled,
        Fulfilled,
        Cancelled,
        Refunded
    }
}
