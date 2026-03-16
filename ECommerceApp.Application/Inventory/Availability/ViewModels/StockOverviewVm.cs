using System.Collections.Generic;

namespace ECommerceApp.Application.Inventory.Availability.ViewModels
{
    public sealed class StockOverviewVm
    {
        public IReadOnlyList<StockOverviewItemVm> Items { get; init; } = new List<StockOverviewItemVm>();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    public sealed class StockOverviewItemVm
    {
        public int ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public int Quantity { get; init; }
        public int ReservedQuantity { get; init; }
        public int AvailableQuantity { get; init; }
        public bool IsDigital { get; init; }
        public string CatalogStatus { get; init; } = "";
        public bool HasPendingAdjustment { get; init; }
        public int? PendingNewQuantity { get; init; }
    }
}
