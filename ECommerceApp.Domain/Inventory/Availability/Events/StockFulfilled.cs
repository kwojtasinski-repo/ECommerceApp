using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockFulfilled(StockItemId StockItemId, int ProductId, int Quantity, DateTime OccurredAt);
}
