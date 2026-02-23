using System;

namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record PhoneNumber
    {
        public string Value { get; }

        public PhoneNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("PhoneNumber cannot be empty", nameof(value));
            var normalized = value.Trim().Replace(" ", "");
            if (normalized.Length > 20)
                throw new ArgumentException("PhoneNumber cannot exceed 20 characters", nameof(value));
            Value = normalized;
        }

        public override string ToString() => Value;
    }
}
