using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.Currencies.ValueObjects
{
    public sealed record CurrencyCode
    {
        public string Value { get; }

        public CurrencyCode(string value)
        {
            var trimmed = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Currency code is required.");
            if (trimmed.Length != 3)
                throw new DomainException("Currency code must be exactly 3 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
