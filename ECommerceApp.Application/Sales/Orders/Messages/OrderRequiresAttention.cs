using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Orders.Messages
{
    public record OrderRequiresAttention(
        int OrderId,
        string Reason,
        DateTime OccurredAt) : IMessage;
}
