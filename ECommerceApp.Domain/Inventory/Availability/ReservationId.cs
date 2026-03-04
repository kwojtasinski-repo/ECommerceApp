using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public sealed record ReservationId(int Value) : TypedId<int>(Value);
}
