using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.DTOs
{
    public sealed record PlaceOrderFromPresaleDto(
        int CustomerId,
        int CurrencyId,
        string UserId,
        IReadOnlyList<PlaceOrderLineDto> Lines);
}
