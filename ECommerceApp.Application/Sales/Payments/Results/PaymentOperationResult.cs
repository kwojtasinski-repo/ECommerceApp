namespace ECommerceApp.Application.Sales.Payments.Services
{
    public enum PaymentOperationResult
    {
        Success,
        PaymentNotFound,
        AlreadyConfirmed,
        AlreadyExpired,
        AlreadyRefunded
    }
}
