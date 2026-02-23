namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record BuildingNumber
    {
        public string Value { get; }

        public BuildingNumber(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Building number is required.");
            if (trimmed.Length > 20)
                throw new DomainException("Building number must not exceed 20 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
