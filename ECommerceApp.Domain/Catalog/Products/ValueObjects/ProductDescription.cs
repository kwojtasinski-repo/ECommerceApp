using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record ProductDescription
    {
        public string Value { get; }

        public ProductDescription(string value)
        {
            var trimmed = value?.Trim() ?? "";
            if (trimmed.Length > 300)
                throw new DomainException("Description must not exceed 300 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
