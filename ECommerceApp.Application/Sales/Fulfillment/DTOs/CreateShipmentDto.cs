using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.DTOs
{
    public record CreateShipmentDto(int OrderId, IReadOnlyList<CreateShipmentLineDto> Lines);
}
