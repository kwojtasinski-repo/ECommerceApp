using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed class OrderProductSnapshot
    {
        public string ProductName { get; private set; } = default!;
        public string? ImageFileName { get; private set; }
        public int? ImageId { get; private set; }

        private OrderProductSnapshot() { }

        public OrderProductSnapshot(string productName, string? imageFileName, int? imageId)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new DomainException("OrderProductSnapshot.ProductName is required.");
            }

            ProductName = productName;
            ImageFileName = imageFileName;
            ImageId = imageId;
        }
    }
}
