using System;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record SoftReservation(
        int ProductId,
        string UserId,
        int Quantity,
        DateTime ExpiresAt);
}
