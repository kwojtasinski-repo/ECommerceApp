using System;

namespace ECommerceApp.Domain.AccountProfile.ValueObjects
{
    public sealed record CompanyName
    {
        public string Value { get; }

        public CompanyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("CompanyName cannot be empty", nameof(value));
            if (value.Length > 300)
                throw new ArgumentException("CompanyName cannot exceed 300 characters", nameof(value));
            Value = value.Trim();
        }

        public override string ToString() => Value;
    }
}
