using ECommerceApp.Domain.Inventory.Availability.Events;
using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class StockItem
    {
        public StockItemId Id { get; private set; }
        public int ProductId { get; private set; }
        public int Quantity { get; private set; }
        public int ReservedQuantity { get; private set; }
        public byte[] RowVersion { get; private set; } = default!;

        public int AvailableQuantity => Quantity - ReservedQuantity;

        private StockItem() { }

        public static (StockItem, StockAdjusted) Create(int productId, int initialQuantity)
        {
            if (productId <= 0)
                throw new DomainException("ProductId must be positive.");
            if (initialQuantity < 0)
                throw new DomainException("Initial quantity cannot be negative.");

            var stock = new StockItem
            {
                ProductId = productId,
                Quantity = initialQuantity,
                ReservedQuantity = 0
            };
            return (stock, new StockAdjusted(stock.Id, productId, 0, initialQuantity, DateTime.UtcNow));
        }

        public StockReserved Reserve(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Reserve quantity must be positive.");
            if (quantity > AvailableQuantity)
                throw new DomainException(
                    $"Cannot reserve {quantity} — only {AvailableQuantity} available.");

            ReservedQuantity += quantity;
            return new StockReserved(Id, ProductId, quantity, DateTime.UtcNow);
        }

        public StockReleased Release(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Release quantity must be positive.");
            if (quantity > ReservedQuantity)
                throw new DomainException(
                    $"Cannot release {quantity} — only {ReservedQuantity} reserved.");

            ReservedQuantity -= quantity;
            return new StockReleased(Id, ProductId, quantity, DateTime.UtcNow);
        }

        public StockFulfilled Fulfill(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Fulfill quantity must be positive.");
            if (quantity > ReservedQuantity)
                throw new DomainException(
                    $"Cannot fulfill {quantity} — only {ReservedQuantity} reserved.");

            ReservedQuantity -= quantity;
            Quantity -= quantity;
            return new StockFulfilled(Id, ProductId, quantity, DateTime.UtcNow);
        }

        public StockReturned Return(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Return quantity must be positive.");

            Quantity += quantity;
            return new StockReturned(Id, ProductId, quantity, DateTime.UtcNow);
        }

        public StockAdjusted Adjust(int newQuantity)
        {
            if (newQuantity < 0)
                throw new DomainException("Stock quantity cannot be negative.");
            if (newQuantity < ReservedQuantity)
                throw new DomainException(
                    $"Cannot adjust to {newQuantity} — {ReservedQuantity} units currently reserved.");

            var previous = Quantity;
            Quantity = newQuantity;
            return new StockAdjusted(Id, ProductId, previous, newQuantity, DateTime.UtcNow);
        }
    }
}
