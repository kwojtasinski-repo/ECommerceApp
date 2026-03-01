using ECommerceApp.Domain.Shared;
using ISO._4217;
using System.Linq;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Supporting.Currencies.ValueObjects
{
    public sealed record CurrencyCode
    {
        public string Value { get; }

        public static bool IsKnownIso4217Code(string? code)
        {
            var normalized = code?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalized))
                return false;
            return CurrencyCodesResolver.GetCurrenciesByCode(normalized).Any();
        }

        public static IReadOnlyCollection<string> GetAllCodes()
            => CurrencyCodesResolver.Codes
                .Where(c => !string.IsNullOrEmpty(c.Code) && c.Code.Length == 3)
                .Select(c => c.Code)
                .Distinct()
                .OrderBy(c => c)
                .ToList()
                .AsReadOnly();

        public CurrencyCode(string value)
        {
            var trimmed = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Currency code is required.");
            if (trimmed.Length != 3)
                throw new DomainException("Currency code must be exactly 3 characters.");
            if (!IsKnownIso4217Code(trimmed))
                throw new DomainException($"'{trimmed}' is not a valid ISO 4217 currency code.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
