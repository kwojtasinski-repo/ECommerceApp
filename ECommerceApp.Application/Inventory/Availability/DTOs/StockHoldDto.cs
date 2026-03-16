using System;

namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public sealed record StockHoldDto(
        int Id,
        int ProductId,
        int OrderId,
        int Quantity,
        string Status,
        DateTime ReservedAt,
        DateTime ExpiresAt);
}
