using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class PendingStockAdjustment
    {
        public int ProductId { get; private set; }
        public int NewQuantity { get; private set; }
        public Guid Version { get; private set; }
        public DateTime SubmittedAt { get; private set; }

        private PendingStockAdjustment() { }

        public static PendingStockAdjustment Create(int productId, int newQuantity)
            => new PendingStockAdjustment
            {
                ProductId = productId,
                NewQuantity = newQuantity,
                Version = Guid.NewGuid(),
                SubmittedAt = DateTime.UtcNow
            };
    }
}
