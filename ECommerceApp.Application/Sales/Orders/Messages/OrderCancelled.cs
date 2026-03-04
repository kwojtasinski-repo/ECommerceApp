using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.Messages
{
    public record OrderCancelled(
        int OrderId,
        IReadOnlyList<OrderCancelledItem> Items,
        DateTime OccurredAt) : IMessage;

    public record OrderCancelledItem(int ProductId, int Quantity);
}
