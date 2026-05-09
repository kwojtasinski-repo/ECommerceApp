using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeOrderListVm
    {
        public IReadOnlyList<BackofficeOrderItemVm> Orders { get; init; } = new List<BackofficeOrderItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string SearchString { get; init; }
    }

    public sealed class BackofficeOrderItemVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string Status { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public bool IsPaid { get; init; }
    }

    public sealed class BackofficeOrderDetailVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string Status { get; init; } = string.Empty;
        public int CustomerId { get; init; }
        public bool IsPaid { get; init; }
        public bool IsDelivered { get; init; }
    }
}
