namespace ECommerceApp.Domain.Inventory.Availability
{
    public class ProductSnapshot
    {
        public int ProductId { get; private set; }
        public string ProductName { get; private set; } = default!;
        public bool IsDigital { get; private set; }
        public CatalogProductStatus CatalogStatus { get; private set; }

        public bool CanBeReserved => CatalogStatus == CatalogProductStatus.Orderable;

        private ProductSnapshot() { }

        public static ProductSnapshot Create(int productId, string productName, bool isDigital, CatalogProductStatus status)
            => new ProductSnapshot
            {
                ProductId = productId,
                ProductName = productName,
                IsDigital = isDigital,
                CatalogStatus = status
            };
    }
}
