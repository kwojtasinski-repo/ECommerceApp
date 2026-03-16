using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public sealed record StockHoldId(int Value) : TypedId<int>(Value);
}
