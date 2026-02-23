namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record Street
    {
        public string Value { get; }

        public Street(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Street is required.");
            if (trimmed.Length > 200)
                throw new DomainException("Street must not exceed 200 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
