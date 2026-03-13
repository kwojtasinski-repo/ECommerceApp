using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record RefundRejected(
        int RefundId,
        int OrderId,
        DateTime OccurredAt) : IMessage;
}
