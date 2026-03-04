using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Payments.Messages
{
    public record RefundApproved(
        int OrderId,
        int ProductId,
        int Quantity,
        DateTime OccurredAt) : IMessage;
}
