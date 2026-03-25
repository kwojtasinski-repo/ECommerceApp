using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.DTOs
{
    public record RequestRefundDto(
        int OrderId,
        string Reason,
        bool OnWarranty,
        IReadOnlyList<RequestRefundItemDto> Items,
        string UserId);
}
