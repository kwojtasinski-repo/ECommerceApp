using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.ViewModels
{
    public sealed class OrderListVm
    {
        public IReadOnlyList<OrderForListVm> Orders { get; init; } = new List<OrderForListVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string? SearchString { get; init; }
    }
}
