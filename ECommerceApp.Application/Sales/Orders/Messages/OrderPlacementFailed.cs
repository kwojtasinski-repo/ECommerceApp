using ECommerceApp.Application.Messaging;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.Messages
{
    public record OrderPlacementFailed(
        int OrderId,
        string Reason,
        IReadOnlyList<OrderPlacedItem> Items,
        string UserId) : IMessage;
}
