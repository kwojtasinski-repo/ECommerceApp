using System.Collections.Generic;

namespace ECommerceApp.Domain.Sales.Orders.Events.Payloads
{
    public record PartialFulfilmentPayload(IReadOnlyList<FulfilledItem> Items);
    public record FulfilledItem(int ItemId, int Quantity);
}
