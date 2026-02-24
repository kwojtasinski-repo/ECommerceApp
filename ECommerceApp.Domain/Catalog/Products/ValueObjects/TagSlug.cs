namespace ECommerceApp.Domain.Catalog.Products.ValueObjects
{
    public sealed record TagSlug
    {
        public string Value { get; }

        public TagSlug(string value)
        {
            var slug = new Slug(value);
            if (slug.Value.Length > 30)
                throw new Shared.DomainException("Tag slug must not exceed 30 characters. Use a shorter name.");
            Value = slug.Value;
        }

        public static TagSlug FromName(string name)
        {
            var slug = Slug.FromName(name);
            if (slug.Value.Length > 30)
                throw new Shared.DomainException("Tag slug exceeds 30 characters. Use a shorter name.");
            return new TagSlug(slug.Value);
        }

        public override string ToString() => Value;
    }
}
