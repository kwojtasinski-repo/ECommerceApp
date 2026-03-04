using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class Reservation
    {
        public ReservationId Id { get; private set; }
        public int ProductId { get; private set; }
        public int OrderId { get; private set; }
        public int Quantity { get; private set; }
        public ReservationStatus Status { get; private set; }
        public DateTime ReservedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        private Reservation() { }

        public static Reservation Create(int productId, int orderId, int quantity, DateTime expiresAt)
            => new Reservation
            {
                ProductId = productId,
                OrderId = orderId,
                Quantity = quantity,
                Status = ReservationStatus.Guaranteed,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };

        public void Confirm() => Status = ReservationStatus.Confirmed;
    }
}
