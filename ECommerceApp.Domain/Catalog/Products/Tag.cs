using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Tag
    {
        public TagId Id { get; private set; } = new TagId(0);
        public TagName Name { get; private set; } = default!;
        public Slug Slug { get; private set; } = default!;
        public string Color { get; private set; }
        public bool IsVisible { get; private set; }

        private Tag() { }

        public static Tag Create(string name, string color = null, bool isVisible = true)
        {
            var tagName = new TagName(name);
            var slug = Slug.FromName(tagName.Value);
            if (slug.Value.Length > 30)
                throw new DomainException("Tag slug exceeds 30 characters. Use a shorter name.");

            return new Tag
            {
                Name = tagName,
                Slug = slug,
                Color = color?.Trim(),
                IsVisible = isVisible
            };
        }

        public void Update(string name, string color, bool isVisible)
        {
            var tagName = new TagName(name);
            var slug = Slug.FromName(tagName.Value);
            if (slug.Value.Length > 30)
                throw new DomainException("Tag slug exceeds 30 characters. Use a shorter name.");

            Name = tagName;
            Slug = slug;
            Color = color?.Trim();
            IsVisible = isVisible;
        }

        public void SetVisibility(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }
}
