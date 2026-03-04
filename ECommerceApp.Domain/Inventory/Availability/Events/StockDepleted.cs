using System;

namespace ECommerceApp.Domain.Inventory.Availability.Events
{
    public record StockDepleted(StockItemId StockItemId, int ProductId, DateTime OccurredAt);
}
