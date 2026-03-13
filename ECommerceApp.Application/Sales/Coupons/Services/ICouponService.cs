using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.Results;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    public interface ICouponService
    {
        Task<CouponApplyResult> ApplyCouponAsync(string couponCode, int orderId, CancellationToken ct = default);
        Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default);
    }
}
