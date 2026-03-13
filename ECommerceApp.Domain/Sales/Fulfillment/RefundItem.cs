using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public class RefundItem
    {
        public int Id { get; private set; }
        public int ProductId { get; private set; }
        public int Quantity { get; private set; }

        private RefundItem() { }

        public static RefundItem Create(int productId, int quantity)
        {
            if (productId <= 0)
            {
                throw new DomainException("ProductId must be positive.");
            }

            if (quantity <= 0)
            {
                throw new DomainException("Quantity must be positive.");
            }

            return new RefundItem { ProductId = productId, Quantity = quantity };
        }
    }
}
