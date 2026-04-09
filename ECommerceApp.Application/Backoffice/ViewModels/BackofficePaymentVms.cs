using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficePaymentListVm
    {
        public IReadOnlyList<BackofficePaymentItemVm> Payments { get; init; } = new List<BackofficePaymentItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
    }

    public sealed class BackofficePaymentItemVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string State { get; init; } = string.Empty;
        public int OrderId { get; init; }
    }

    public sealed class BackofficePaymentDetailVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Cost { get; init; }
        public string State { get; init; } = string.Empty;
        public int OrderId { get; init; }
        public int CustomerId { get; init; }
    }
}
