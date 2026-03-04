using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.Messages
{
    public record OrderShipped(
        int OrderId,
        IReadOnlyList<OrderShippedItem> Items,
        DateTime OccurredAt) : IMessage;

    public record OrderShippedItem(int ProductId, int Quantity);
}
