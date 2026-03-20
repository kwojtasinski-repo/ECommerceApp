using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public class ShipmentLine
    {
        public int Id { get; private set; }
        public int ProductId { get; private set; }
        public int Quantity { get; private set; }

        private ShipmentLine() { }

        public static ShipmentLine Create(int productId, int quantity)
        {
            if (productId <= 0)
                throw new DomainException("ProductId must be positive.");

            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");

            return new ShipmentLine { ProductId = productId, Quantity = quantity };
        }
    }
}
