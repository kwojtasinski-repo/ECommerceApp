using System;

namespace ECommerceApp.Domain.Sales.Payments.Events
{
    public record RefundIssuedEvent(int PaymentId, int OrderId, int ProductId, int Quantity, DateTime OccurredAt);
}
