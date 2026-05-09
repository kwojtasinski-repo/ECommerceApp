using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.ViewModels
{
    public sealed record ShipmentLineVm(int ProductId, int Quantity);

    public sealed record ShipmentDetailsVm(
        int Id,
        int OrderId,
        string TrackingNumber,
        string Status,
        DateTime? ShippedAt,
        DateTime? DeliveredAt,
        IReadOnlyList<ShipmentLineVm> Lines);
}
