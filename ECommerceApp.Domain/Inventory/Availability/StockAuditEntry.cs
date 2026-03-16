using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class StockAuditEntry
    {
        public StockAuditEntryId Id { get; private set; }
        public int ProductId { get; private set; }
        public StockChangeType ChangeType { get; private set; }
        public int QuantityBefore { get; private set; }
        public int QuantityAfter { get; private set; }
        public int? OrderId { get; private set; }
        public DateTime OccurredAt { get; private set; }

        public int Delta => QuantityAfter - QuantityBefore;

        private StockAuditEntry() { }

        public static StockAuditEntry Create(
            int productId,
            StockChangeType changeType,
            int quantityBefore,
            int quantityAfter,
            int? orderId,
            DateTime occurredAt)
            => new StockAuditEntry
            {
                ProductId      = productId,
                ChangeType     = changeType,
                QuantityBefore = quantityBefore,
                QuantityAfter  = quantityAfter,
                OrderId        = orderId,
                OccurredAt     = occurredAt
            };
    }
}
