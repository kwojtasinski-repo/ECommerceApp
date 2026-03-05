using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability.ValueObjects
{
    public sealed record StockProductId : TypedId<int>
    {
        public StockProductId(int value) : base(value)
        {
            if (value <= 0)
                throw new DomainException("ProductId must be positive.");
        }
    }
}
