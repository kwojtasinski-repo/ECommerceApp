using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Payments.Messages
{
    public record PaymentExpired(
        int PaymentId,
        int OrderId,
        DateTime OccurredAt,
        Guid CorrelationId = default) : IMessage;
}
