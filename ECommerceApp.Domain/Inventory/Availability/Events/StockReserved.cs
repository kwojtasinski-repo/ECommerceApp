using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockReserved(StockItemId StockItemId, int ProductId, int Quantity, DateTime OccurredAt);
}
