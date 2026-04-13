using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using ECommerceApp.Domain.Shared;
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

        public void Confirm()
        {
            if (Status == StockHoldStatus.Released || Status == StockHoldStatus.Fulfilled || Status == StockHoldStatus.Withdrawn)
                throw new DomainException($"Cannot confirm a hold in '{Status}' status.");
            Status = StockHoldStatus.Confirmed;
        }

        public void MarkAsReleased()
        {
            if (Status == StockHoldStatus.Fulfilled || Status == StockHoldStatus.Withdrawn)
                throw new DomainException($"Cannot release a hold in '{Status}' status.");
            Status = StockHoldStatus.Released;
        }

        public void MarkAsFulfilled()
        {
            if (Status == StockHoldStatus.Released || Status == StockHoldStatus.Withdrawn)
                throw new DomainException($"Cannot fulfill a hold in '{Status}' status.");
            Status = StockHoldStatus.Fulfilled;
        }

        public void Withdraw()
        {
            if (Status == StockHoldStatus.Released || Status == StockHoldStatus.Fulfilled || Status == StockHoldStatus.Withdrawn)
                throw new DomainException($"Cannot withdraw a hold in '{Status}' status.");
            Status = StockHoldStatus.Withdrawn;
        }
    }
}
