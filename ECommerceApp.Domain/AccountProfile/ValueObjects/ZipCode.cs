namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record ZipCode
    {
        public string Value { get; }

        public ZipCode(string value)
        {
            Value = value?.Trim() ?? throw new DomainException("ZIP code is required.");
            if (Value.Length < 2 || Value.Length > 12)
                throw new DomainException("ZIP code length must be 2-12 characters.");
            if (string.IsNullOrWhiteSpace(Value.Replace(" ", "")))
                throw new DomainException("ZIP code cannot be only whitespace.");
        }

        public override string ToString() => Value;
    }
}
