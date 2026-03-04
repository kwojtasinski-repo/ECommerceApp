using System;

namespace ECommerceApp.Application.Inventory.Availability.DTOs
{
    public sealed record SoftHold(int ProductId, string UserId, int Quantity, DateTime ExpiresAt);
}
