using System;
using System.Text.RegularExpressions;

namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record Email
    {
        private static readonly Regex _pattern =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Value { get; }

        public Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Email cannot be empty", nameof(value));
            }

            if (!IsValid(value))
            {
                throw new ArgumentException("Email format is invalid", nameof(value));
            }

            Value = value.Trim().ToLowerInvariant();
        }

        public static bool IsValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && _pattern.IsMatch(value);
        }

        public override string ToString() => Value;
    }
}
