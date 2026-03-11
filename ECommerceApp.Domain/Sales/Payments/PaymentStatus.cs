namespace ECommerceApp.Domain.Sales.Payments
{
    public enum PaymentStatus
    {
        Pending,    // payment initialized — awaiting customer payment
        Confirmed,  // gateway confirmed — stock reservation upgraded
        Expired,    // payment window closed without payment
        Refunded    // confirmed payment reversed
    }
}
