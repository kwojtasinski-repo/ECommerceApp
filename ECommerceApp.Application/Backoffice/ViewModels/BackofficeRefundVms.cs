using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeRefundListVm
    {
        public IReadOnlyList<BackofficeRefundItemVm> Refunds { get; init; } = new List<BackofficeRefundItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
    }

    public sealed class BackofficeRefundItemVm
    {
        public int Id { get; init; }
        public int OrderId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool OnWarranty { get; init; }
    }

    public sealed class BackofficeRefundDetailVm
    {
        public int Id { get; init; }
        public int OrderId { get; init; }
        public int CustomerId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool OnWarranty { get; init; }
    }
}
