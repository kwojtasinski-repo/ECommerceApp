namespace ECommerceApp.Application.Sales.Orders.Results
{
    public enum OrderOperationResult
    {
        Success,
        OrderNotFound,
        AlreadyPaid,
        NotPaid,
        NotDelivered,
        AlreadyDelivered,
        CouponNotAssigned,
        AlreadyCancelled
    }
}
