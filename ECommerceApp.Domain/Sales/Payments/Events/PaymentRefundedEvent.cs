using System;

namespace ECommerceApp.Domain.Sales.Payments.Events
{
    public record PaymentRefundedEvent(int PaymentId, int OrderId, int RefundId, DateTime OccurredAt);
}
