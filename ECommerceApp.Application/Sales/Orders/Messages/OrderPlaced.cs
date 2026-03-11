using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.Messages
{
    public record OrderPlaced(
        int OrderId,
        IReadOnlyList<OrderPlacedItem> Items,
        string UserId,
        DateTime ExpiresAt,
        DateTime OccurredAt,
        decimal TotalAmount,
        int CurrencyId) : IMessage;

    public record OrderPlacedItem(int ProductId, int Quantity);
}
