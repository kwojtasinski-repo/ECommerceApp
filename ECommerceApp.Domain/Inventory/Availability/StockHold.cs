using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class StockHold
    {
        public StockHoldId Id { get; private set; }
        public StockProductId ProductId { get; private set; }
        public ReservationOrderId OrderId { get; private set; }
        public int Quantity { get; private set; }
        public StockHoldStatus Status { get; private set; }
        public DateTime ReservedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        private StockHold() { }

        public static StockHold Create(StockProductId productId, ReservationOrderId orderId, int quantity, DateTime expiresAt)
            => new StockHold
            {
                ProductId  = productId,
                OrderId    = orderId,
                Quantity   = quantity,
                Status     = StockHoldStatus.Guaranteed,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt  = expiresAt
            };

        public bool IsGuaranteed => Status == StockHoldStatus.Guaranteed;

        public void Confirm()         => Status = StockHoldStatus.Confirmed;
        public void MarkAsReleased()  => Status = StockHoldStatus.Released;
        public void MarkAsFulfilled() => Status = StockHoldStatus.Fulfilled;
    }
}
