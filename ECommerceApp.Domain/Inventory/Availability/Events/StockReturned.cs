using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockReturned(StockItemId StockItemId, int ProductId, int Quantity, DateTime OccurredAt);
}
