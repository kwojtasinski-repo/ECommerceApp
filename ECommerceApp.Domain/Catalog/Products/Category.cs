using ECommerceApp.Domain.Catalog.Products.ValueObjects;

namespace ECommerceApp.Domain.Catalog.Products
{
    public class Category
    {
        public CategoryId Id { get; private set; }
        public CategoryName Name { get; private set; } = default!;
        public CategorySlug Slug { get; private set; } = default!;

        private Category() { }

        public static Category Create(string name)
        {
            var categoryName = new CategoryName(name);
            return new Category
            {
                Name = categoryName,
                Slug = CategorySlug.FromName(categoryName.Value)
            };
        }

        public void Update(string name)
        {
            var categoryName = new CategoryName(name);
            Name = categoryName;
            Slug = CategorySlug.FromName(categoryName.Value);
        }
    }
}
