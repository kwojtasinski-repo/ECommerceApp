using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public class OrderItem
    {
        public OrderItemId Id { get; private set; }
        public OrderProductId ItemId { get; private set; }
        public int Quantity { get; private set; }
        public UnitCost UnitCost { get; private set; } = default!;
        public OrderUserId UserId { get; private set; }
        public OrderId? OrderId { get; private set; }
        public int? CouponUsedId { get; private set; }
        public OrderProductSnapshot? Snapshot { get; private set; }

        private OrderItem() { }

        public static OrderItem Create(OrderProductId itemId, int quantity, UnitCost unitCost, OrderUserId userId)
        {
            if (itemId is null || itemId.Value <= 0)
                throw new DomainException("ItemId must be positive.");
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");
            if (unitCost is null)
                throw new DomainException("UnitCost is required.");
            if (userId is null || string.IsNullOrWhiteSpace(userId.Value))
                throw new DomainException("UserId is required.");

            return new OrderItem
            {
                ItemId = itemId,
                Quantity = quantity,
                UnitCost = unitCost,
                UserId = userId
            };
        }

        public void SetSnapshot(OrderProductSnapshot snapshot)
        {
            if (snapshot is null)
                throw new DomainException("OrderProductSnapshot (snapshot) is required.");
            Snapshot = snapshot;
        }

        public void UpdateQuantity(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");
            Quantity = quantity;
        }

        public void ApplyCoupon(int couponUsedId)
        {
            if (couponUsedId <= 0)
                throw new DomainException("CouponUsedId must be positive.");
            CouponUsedId = couponUsedId;
        }

        public void RemoveCoupon()
        {
            CouponUsedId = null;
        }
    }
}
