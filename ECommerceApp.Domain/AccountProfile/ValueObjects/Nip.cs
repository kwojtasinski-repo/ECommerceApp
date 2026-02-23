using System;
using System.Linq;

namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record Nip
    {
        public string Value { get; }

        public Nip(string value)
        {
            if (!IsValid(value))
                throw new ArgumentException("NIP is invalid. Must be 10 digits with a valid checksum.", nameof(value));
            Value = value.Trim().Replace("-", "").Replace(" ", "");
        }

        private static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var digits = value.Trim().Replace("-", "").Replace(" ", "");
            if (digits.Length != 10 || !digits.All(char.IsDigit)) return false;

            int[] weights = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
            var sum = 0;
            for (var i = 0; i < weights.Length; i++)
                sum += weights[i] * (digits[i] - '0');

            var checkDigit = sum % 11;
            return checkDigit != 10 && checkDigit == (digits[9] - '0');
        }

        public override string ToString() => Value;
    }
}
