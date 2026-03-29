using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.ViewModels;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    public interface ICouponService
    {
        Task<CouponApplyResult> ApplyCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default);
        Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default);
        Task<CouponApplicationResult> CreateCouponAsync(CreateCouponDto dto, CancellationToken ct = default);
        Task<bool> AddCouponAsync(string code, string description, CancellationToken ct = default);
        Task<CouponListVm> GetCouponsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<CouponDetailVm?> GetCouponAsync(int id, CancellationToken ct = default);
        Task<bool> UpdateCouponAsync(UpdateCouponDto dto, CancellationToken ct = default);
        Task<bool> DeleteCouponAsync(int id, CancellationToken ct = default);
        Task<CouponRulePipelineResult> SimulateCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default);
    }
}
