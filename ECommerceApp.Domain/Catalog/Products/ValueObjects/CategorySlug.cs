namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record CategorySlug
    {
        public string Value { get; }

        public CategorySlug(string value)
        {
            var slug = new Slug(value);
            if (slug.Value.Length > 100)
                throw new Shared.DomainException("Category slug must not exceed 100 characters. Use a shorter name.");
            Value = slug.Value;
        }

        public static CategorySlug FromName(string name)
        {
            var slug = Slug.FromName(name);
            if (slug.Value.Length > 100)
                throw new Shared.DomainException("Category slug exceeds 100 characters. Use a shorter name.");
            return new CategorySlug(slug.Value);
        }

        public override string ToString() => Value;
    }
}
