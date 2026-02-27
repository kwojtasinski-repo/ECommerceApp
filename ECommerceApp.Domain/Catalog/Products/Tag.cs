using ECommerceApp.Domain.Catalog.Products.ValueObjects;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Tag
    {
        public TagId Id { get; private set; }
        public TagName Name { get; private set; } = default!;
        public TagSlug Slug { get; private set; } = default!;

        private Tag() { }

        public static Tag Create(string name)
        {
            var tagName = new TagName(name);
            return new Tag
            {
                Name = tagName,
                Slug = TagSlug.FromName(tagName.Value)
            };
        }

        public void Update(string name)
        {
            var tagName = new TagName(name);
            Name = tagName;
            Slug = TagSlug.FromName(tagName.Value);
        }
    }
}
