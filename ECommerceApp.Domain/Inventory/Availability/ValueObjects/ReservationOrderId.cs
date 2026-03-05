using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability.ValueObjects
{
    public sealed record ReservationOrderId : TypedId<int>
    {
        public ReservationOrderId(int value) : base(value)
        {
            if (value <= 0)
                throw new DomainException("OrderId must be positive.");
        }
    }
}
