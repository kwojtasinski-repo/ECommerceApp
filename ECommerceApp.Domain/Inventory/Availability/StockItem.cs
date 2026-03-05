using ECommerceApp.Domain.Inventory.Availability.Events;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public class StockItem
    {
        public StockItemId Id { get; private set; }
        public StockProductId ProductId { get; private set; }
        public StockQuantity Quantity { get; private set; }
        public StockQuantity ReservedQuantity { get; private set; }
        public byte[] RowVersion { get; private set; } = default!;

        public int AvailableQuantity => Quantity.Value - ReservedQuantity.Value;

        private StockItem() { }

        public static (StockItem, StockAdjusted) Create(StockProductId productId, StockQuantity initialQuantity)
        {
            var stock = new StockItem
            {
                ProductId = productId,
                Quantity = initialQuantity,
                ReservedQuantity = new StockQuantity(0)
            };
            return (stock, new StockAdjusted(stock.Id, productId.Value, 0, initialQuantity.Value, DateTime.UtcNow));
        }

        public StockReserved Reserve(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Reserve quantity must be positive.");
            if (quantity > AvailableQuantity)
                throw new DomainException(
                    $"Cannot reserve {quantity} — only {AvailableQuantity} available.");

            ReservedQuantity = new StockQuantity(ReservedQuantity.Value + quantity);
            return new StockReserved(Id, ProductId.Value, quantity, DateTime.UtcNow);
        }

        public bool CanRelease(int qty) => qty > 0 && qty <= ReservedQuantity.Value;

        public StockReleased Release(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Release quantity must be positive.");
            if (quantity > ReservedQuantity.Value)
                throw new DomainException(
                    $"Cannot release {quantity} — only {ReservedQuantity} reserved.");

            ReservedQuantity = new StockQuantity(ReservedQuantity.Value - quantity);
            return new StockReleased(Id, ProductId.Value, quantity, DateTime.UtcNow);
        }

        public bool CanFulfill(int qty) => qty > 0 && qty <= ReservedQuantity.Value;

        public StockFulfilled Fulfill(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Fulfill quantity must be positive.");
            if (quantity > ReservedQuantity.Value)
                throw new DomainException(
                    $"Cannot fulfill {quantity} — only {ReservedQuantity} reserved.");

            ReservedQuantity = new StockQuantity(ReservedQuantity.Value - quantity);
            Quantity = new StockQuantity(Quantity.Value - quantity);
            return new StockFulfilled(Id, ProductId.Value, quantity, DateTime.UtcNow);
        }

        public StockReturned Return(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Return quantity must be positive.");

            Quantity = new StockQuantity(Quantity.Value + quantity);
            return new StockReturned(Id, ProductId.Value, quantity, DateTime.UtcNow);
        }

        public StockAdjusted Adjust(StockQuantity newQuantity)
        {
            if (newQuantity.Value < ReservedQuantity.Value)
                throw new DomainException(
                    $"Cannot adjust to {newQuantity} — {ReservedQuantity} units currently reserved.");

            var previous = Quantity.Value;
            Quantity = newQuantity;
            return new StockAdjusted(Id, ProductId.Value, previous, newQuantity.Value, DateTime.UtcNow);
        }
    }
}
