using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Coupons.Services;
using System;
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

        public Task<BackofficeCouponListVm> GetCouponsAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeCouponDetailVm?> GetCouponDetailAsync(int couponId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
