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
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string? SearchString { get; init; }
    }
}
