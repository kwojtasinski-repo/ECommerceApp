using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Category
    {
        public CategoryId Id { get; private set; } = new CategoryId(0);
        public CategoryName Name { get; private set; } = default!;
        public Slug Slug { get; private set; } = default!;

        private Category() { }

        public static Category Create(string name)
        {
            var categoryName = new CategoryName(name);
            var slug = Slug.FromName(categoryName.Value);
            if (slug.Value.Length > 100)
                throw new DomainException("Category slug exceeds 100 characters. Use a shorter name.");

            return new Category
            {
                Name = categoryName,
                Slug = slug
            };
        }

        public void Update(string name)
        {
            var categoryName = new CategoryName(name);
            var slug = Slug.FromName(categoryName.Value);
            if (slug.Value.Length > 100)
                throw new DomainException("Category slug exceeds 100 characters. Use a shorter name.");

            Name = categoryName;
            Slug = slug;
        }
    }
}
