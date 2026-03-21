using System.Collections.Generic;

namespace ECommerceApp.Domain.Sales.Orders.Events.Payloads
{
    public record PartialFulfilmentPayload(
        int ShipmentId,
        IReadOnlyList<FulfilledItem> DeliveredItems,
        IReadOnlyList<FulfilledItem> FailedItems);
    public record FulfilledItem(int ItemId, int Quantity);
}
