using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Coupons.Services;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCouponService : IBackofficeCouponService
    {
        private readonly ICouponService _couponService;

        public BackofficeCouponService(ICouponService couponService)
        {
            _couponService = couponService;
        }

        public async Task<BackofficeCouponListVm> GetCouponsAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
        {
            var result = await _couponService.GetCouponsAsync(pageSize, pageNo, searchString ?? string.Empty, ct);
            return new BackofficeCouponListVm
            {
                Coupons = result.Coupons.Select(c => new BackofficeCouponItemVm
                {
                    Id = c.Id,
                    Code = c.Code,
                    Description = c.Description,
                    Status = c.Status,
                    UsageCount = 0 // not exposed by ICouponService — tracked as follow-up
                }).ToList(),
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                SearchString = result.SearchString
            };
        }

        public async Task<BackofficeCouponDetailVm?> GetCouponDetailAsync(int couponId, CancellationToken ct = default)
        {
            var coupon = await _couponService.GetCouponAsync(couponId, ct);
            if (coupon is null)
            {
                return null;
            }

            return new BackofficeCouponDetailVm
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Description = coupon.Description,
                Status = coupon.Status,
                UsageCount = 0,    // not exposed by ICouponService — tracked as follow-up
                MaxUsages = null   // not exposed by ICouponService — tracked as follow-up
            };
        }
    }
}
