using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.Currencies.ValueObjects
{
    public sealed record CurrencyDescription
    {
        public string Value { get; }

        public CurrencyDescription(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Currency description is required.");
            if (trimmed.Length > 300)
                throw new DomainException("Currency description must not exceed 300 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
