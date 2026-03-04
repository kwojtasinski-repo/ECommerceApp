using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockAdjusted(StockItemId StockItemId, int ProductId, int PreviousQuantity, int NewQuantity, DateTime OccurredAt);
}
