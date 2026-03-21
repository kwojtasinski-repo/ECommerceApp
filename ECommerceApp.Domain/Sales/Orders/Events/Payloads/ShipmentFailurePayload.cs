using System.Collections.Generic;

namespace ECommerceApp.Domain.Sales.Orders.Events.Payloads
{
    public record ShipmentFailurePayload(int ShipmentId, IReadOnlyList<FailedShipmentItem> Items);
    public record FailedShipmentItem(int ProductId, int Quantity);
}
