using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record TagName
    {
        public string Value { get; }

        public TagName(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Tag name is required.");
            if (trimmed.Length > 50)
                throw new DomainException("Tag name must not exceed 50 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
