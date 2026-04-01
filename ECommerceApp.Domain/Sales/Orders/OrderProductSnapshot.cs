using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed class OrderProductSnapshot
    {
        public string ProductName { get; private set; } = default!;
        public string? ImageFileName { get; private set; }
        public string? ImageUrl { get; private set; }

        private OrderProductSnapshot() { }

        public OrderProductSnapshot(string productName, string? imageFileName, string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new DomainException("OrderProductSnapshot.ProductName is required.");
            }

            ProductName = productName;
            ImageFileName = imageFileName;
            ImageUrl = imageUrl;
        }
    }
}
