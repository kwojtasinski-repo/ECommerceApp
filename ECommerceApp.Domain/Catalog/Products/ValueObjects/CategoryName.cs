using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record CategoryName
    {
        public string Value { get; }

        public CategoryName(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Category name is required.");
            if (trimmed.Length > 100)
                throw new DomainException("Category name must not exceed 100 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
