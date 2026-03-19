using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.ViewModels
{
    public sealed record ShipmentVm(
        int Id,
        int OrderId,
        string? TrackingNumber,
        string Status,
        DateTime? ShippedAt,
        DateTime? DeliveredAt);

    public sealed class ShipmentListVm
    {
        public IReadOnlyList<ShipmentVm> Shipments { get; init; } = new List<ShipmentVm>();
    }
}
