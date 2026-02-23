namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record Country
    {
        public string Value { get; }

        public Country(string value)
        {
            var trimmed = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Country is required.");
            if (trimmed.Length != 2)
                throw new DomainException("Country must be an ISO 2-letter code.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
