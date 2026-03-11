namespace ECommerceApp.Application.Sales.Payments.DTOs
{
    public sealed record ConfirmPaymentDto(int PaymentId, string? TransactionRef);
}
