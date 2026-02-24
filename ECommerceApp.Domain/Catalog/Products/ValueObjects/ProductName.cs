using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record ProductName
    {
        public string Value { get; }

        public ProductName(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Product name is required.");
            if (trimmed.Length < 3)
                throw new DomainException("Product name must be at least 3 characters.");
            if (trimmed.Length > 150)
                throw new DomainException("Product name must not exceed 150 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
