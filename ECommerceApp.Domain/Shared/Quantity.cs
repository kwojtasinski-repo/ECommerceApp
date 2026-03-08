namespace ECommerceApp.Domain.Shared
{
    public sealed record Quantity
    {
        public int Value { get; }

        public Quantity(int value)
        {
            if (value <= 0)
                throw new DomainException("Quantity must be positive.");
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
