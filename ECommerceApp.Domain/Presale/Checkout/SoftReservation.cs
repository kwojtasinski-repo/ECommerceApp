using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public class SoftReservation
    {
        public SoftReservationId Id { get; private set; } = default!;
        public PresaleProductId ProductId { get; private set; } = default!;
        public PresaleUserId UserId { get; private set; } = default!;
        public Quantity Quantity { get; private set; } = default!;
        public Price UnitPrice { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public SoftReservationStatus Status { get; private set; }

        private SoftReservation() { }

        public void Commit() => Status = SoftReservationStatus.Committed;
        public void Revert() => Status = SoftReservationStatus.Active;

        public static SoftReservation Create(
            int productId, string userId, int quantity, decimal unitPrice, DateTime expiresAt)
            => new SoftReservation
            {
                ProductId = new PresaleProductId(productId),
                UserId = new PresaleUserId(userId),
                Quantity = new Quantity(quantity),
                UnitPrice = new Price(unitPrice),
                ExpiresAt = expiresAt
            };
    }
}
