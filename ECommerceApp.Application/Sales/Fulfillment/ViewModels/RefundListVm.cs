using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.ViewModels
{
    public sealed class RefundListVm
    {
        public IReadOnlyList<RefundVm> Refunds { get; init; } = new List<RefundVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string? SearchString { get; init; }
    }
}
