using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.DTOs
{
    public sealed record CartDto(
        int CartId,
        string UserId,
        IReadOnlyList<CartItemDto> Items);
}
