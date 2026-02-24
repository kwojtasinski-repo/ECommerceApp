using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record ProductQuantity
    {
        public int Value { get; }

        public ProductQuantity(int value)
        {
            if (value < 0)
                throw new DomainException("Quantity must not be negative.");
            Value = value;
        }

        public static implicit operator int(ProductQuantity qty) => qty.Value;

        public override string ToString() => Value.ToString();
    }
}
