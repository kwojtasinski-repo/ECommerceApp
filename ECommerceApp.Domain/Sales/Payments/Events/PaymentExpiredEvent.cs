using System;

namespace ECommerceApp.Domain.Sales.Payments.Events
{
    public record PaymentExpiredEvent(int PaymentId, int OrderId, DateTime OccurredAt);
}
