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
                throw new ArgumentException("Email cannot be empty", nameof(value));
            if (!_pattern.IsMatch(value))
                throw new ArgumentException("Email format is invalid", nameof(value));
            Value = value.Trim().ToLowerInvariant();
        }

        public override string ToString() => Value;
    }
}
