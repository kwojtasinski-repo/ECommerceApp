using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record ImageFileName
    {
        public string Value { get; }

        public ImageFileName(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Image file name is required.");
            if (trimmed.Length > 500)
                throw new DomainException("Image file name must not exceed 500 characters.");
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
