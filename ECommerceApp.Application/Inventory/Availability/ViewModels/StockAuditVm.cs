using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Inventory.Availability.ViewModels
{
    public sealed class StockAuditVm
    {
        public IReadOnlyList<StockAuditRowVm> Products { get; init; } = new List<StockAuditRowVm>();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    public sealed class StockAuditRowVm
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public string ChangeType { get; init; } = "";
        public int QuantityBefore { get; init; }
        public int QuantityAfter { get; init; }
        public int Delta { get; init; }
        public int? OrderId { get; init; }
        public DateTime OccurredAt { get; init; }
    }
}
