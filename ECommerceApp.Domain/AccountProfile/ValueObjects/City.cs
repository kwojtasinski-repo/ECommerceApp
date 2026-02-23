namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record City
    {
        public string Value { get; }

        public City(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("City is required.");
            if (trimmed.Length > 100)
                throw new DomainException("City must not exceed 100 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
