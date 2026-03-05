using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability.ValueObjects
{
    public sealed record StockQuantity
    {
        public int Value { get; }

        public StockQuantity(int value)
        {
            if (value < 0)
                throw new DomainException("Stock quantity cannot be negative.");
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
