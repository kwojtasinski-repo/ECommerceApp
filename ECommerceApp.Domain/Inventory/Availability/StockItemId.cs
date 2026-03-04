using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public sealed record StockItemId(int Value) : TypedId<int>(Value);
}
