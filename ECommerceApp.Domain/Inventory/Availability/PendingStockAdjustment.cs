using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class PendingStockAdjustment
    {
        public StockProductId ProductId { get; private set; }
        public StockQuantity NewQuantity { get; private set; }
        public Guid Version { get; private set; }
        public DateTime SubmittedAt { get; private set; }

        private PendingStockAdjustment() { }

        public static PendingStockAdjustment Create(StockProductId productId, StockQuantity newQuantity)
            => new PendingStockAdjustment
            {
                ProductId = productId,
                NewQuantity = newQuantity,
                Version = Guid.NewGuid(),
                SubmittedAt = DateTime.UtcNow
            };
    }
}
