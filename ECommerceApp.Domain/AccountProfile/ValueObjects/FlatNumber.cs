namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record FlatNumber
    {
        public int Value { get; }

        public FlatNumber(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
