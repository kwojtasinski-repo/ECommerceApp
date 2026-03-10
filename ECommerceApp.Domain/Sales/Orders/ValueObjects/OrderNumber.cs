using ECommerceApp.Domain.Shared;
using System;
using System.Text.RegularExpressions;

namespace ECommerceApp.Domain.Sales.Orders.ValueObjects
{
    public sealed record OrderNumber
    {
        private static readonly Regex ValidationRegex =
            new(@"^ORD-\d{8}-[A-F0-9]{8}$", RegexOptions.Compiled);

        public string Value { get; }

        private OrderNumber(string value) => Value = value;

        public static OrderNumber Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidationRegex.IsMatch(value))
                throw new DomainException($"Invalid OrderNumber format: '{value}'. Expected: ORD-YYYYMMDD-XXXXXXXX.");
            return new OrderNumber(value);
        }

        public static OrderNumber Generate()
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randPart = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return new OrderNumber($"ORD-{datePart}-{randPart}");
        }

        public static implicit operator string(OrderNumber number) => number.Value;
        public static implicit operator OrderNumber(string value) => Parse(value);

        public override string ToString() => Value;
    }
}
