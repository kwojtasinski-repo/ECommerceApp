using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeCouponService
    {
        Task<BackofficeCouponListVm> GetCouponsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<BackofficeCouponDetailVm> GetCouponDetailAsync(int couponId, CancellationToken ct = default);
    }
}
