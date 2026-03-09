using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public class OrderItem
    {
        public OrderItemId Id { get; private set; }
        public int ItemId { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitCost { get; private set; }
        public string UserId { get; private set; } = default!;
        public int? OrderId { get; private set; }
        public int? CouponUsedId { get; private set; }
        public int? RefundId { get; private set; }

        private OrderItem() { }

        public static OrderItem Create(int itemId, int quantity, decimal unitCost, string userId)
        {
            if (itemId <= 0)
                throw new DomainException("ItemId must be positive.");
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");
            if (unitCost < 0)
                throw new DomainException("UnitCost cannot be negative.");
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("UserId is required.");

            return new OrderItem
            {
                ItemId = itemId,
                Quantity = quantity,
                UnitCost = unitCost,
                UserId = userId
            };
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

        public void AssignRefund(int refundId)
        {
            if (refundId <= 0)
                throw new DomainException("RefundId must be positive.");
            RefundId = refundId;
        }

        public void RemoveRefund()
        {
            RefundId = null;
        }
    }
}
