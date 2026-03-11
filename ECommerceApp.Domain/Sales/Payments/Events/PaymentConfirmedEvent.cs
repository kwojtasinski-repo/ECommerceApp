using System;

namespace ECommerceApp.Domain.Sales.Payments.Events
{
    public record PaymentConfirmedEvent(int PaymentId, int OrderId, DateTime OccurredAt);
}
