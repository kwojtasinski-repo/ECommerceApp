using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.ViewModels
{
    public sealed class OrderItemListVm
    {
        public IReadOnlyList<OrderItemForListVm> Items { get; init; } = new List<OrderItemForListVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string? SearchString { get; init; }
    }

    public sealed class OrderItemForListVm
    {
        public int Id { get; init; }
        public int ItemId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public string UserId { get; init; } = default!;
        public int? OrderId { get; init; }
        public string? ProductName { get; init; }
        public string? ImageFileName { get; init; }
    }
}
