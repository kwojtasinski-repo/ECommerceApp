using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockReleased(StockItemId StockItemId, int ProductId, int Quantity, DateTime OccurredAt);
}
