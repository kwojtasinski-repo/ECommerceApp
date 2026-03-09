using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record PlaceOrderDto(
        int CustomerId,
        int CurrencyId,
        string UserId,
        IReadOnlyList<int> CartItemIds);
}
