using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeCouponListVm
    {
        public IReadOnlyList<BackofficeCouponItemVm> Coupons { get; init; } = new List<BackofficeCouponItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string? SearchString { get; init; }
    }

    public sealed class BackofficeCouponItemVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int UsageCount { get; init; }
    }

    public sealed class BackofficeCouponDetailVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int UsageCount { get; init; }
        public int? MaxUsages { get; init; }
    }
}
