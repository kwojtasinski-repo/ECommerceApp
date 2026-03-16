using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public sealed record StockAuditEntryId(int Value) : TypedId<int>(Value);
}
