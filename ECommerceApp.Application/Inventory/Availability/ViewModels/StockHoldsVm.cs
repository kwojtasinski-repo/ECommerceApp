using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Inventory.Availability.ViewModels
{
    public sealed class StockHoldsVm
    {
        public IReadOnlyList<StockHoldRowVm> Items { get; init; } = new List<StockHoldRowVm>();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public string StatusFilter { get; init; } = "active";
    }

    public sealed class StockHoldRowVm
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public int OrderId { get; init; }
        public int Quantity { get; init; }
        public string Status { get; init; } = "";
        public DateTime ReservedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public bool CanRelease { get; init; }
        public bool CanConfirm { get; init; }
    }
}
