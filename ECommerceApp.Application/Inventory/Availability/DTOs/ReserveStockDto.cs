using System;

namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public sealed record ReserveStockDto(
        int ProductId,
        int OrderId,
        int Quantity,
        string UserId,
        DateTime ExpiresAt);
}
