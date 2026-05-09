using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Coupons.ViewModels
{
    public sealed class CouponListVm
    {
        public IReadOnlyList<CouponForListVm> Coupons { get; init; } = new List<CouponForListVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string SearchString { get; init; }
    }

    public sealed class CouponForListVm
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
