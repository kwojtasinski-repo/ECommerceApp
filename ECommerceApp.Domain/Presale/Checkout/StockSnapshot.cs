using System;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public class StockSnapshot
    {
        public PresaleProductId ProductId { get; private set; } = default!;
        public int AvailableQuantity { get; private set; }
        public DateTime LastSyncedAt { get; private set; }

        private StockSnapshot() { }

        public static StockSnapshot Create(int productId, int availableQuantity, DateTime lastSyncedAt)
            => new StockSnapshot
            {
                ProductId = new PresaleProductId(productId),
                AvailableQuantity = availableQuantity,
                LastSyncedAt = lastSyncedAt
            };

        public void Update(int availableQuantity, DateTime lastSyncedAt)
        {
            AvailableQuantity = availableQuantity;
            LastSyncedAt = lastSyncedAt;
        }
    }
}
