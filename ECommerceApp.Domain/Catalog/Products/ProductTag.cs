namespace ECommerceApp.Domain.Catalog.Products
{
    public class ProductTag
    {
        public ProductId ProductId { get; private set; } = default!;
        public TagId TagId { get; private set; } = default!;

        private ProductTag() { }

        public static ProductTag Create(ProductId productId, TagId tagId)
        {
            return new ProductTag
            {
                ProductId = productId,
                TagId = tagId
            };
        }
    }
}
