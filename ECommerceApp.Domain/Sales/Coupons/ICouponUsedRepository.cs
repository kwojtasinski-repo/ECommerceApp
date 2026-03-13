using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponUsedRepository
    {
        Task<CouponUsed?> FindByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task AddAsync(CouponUsed couponUsed, CancellationToken ct = default);
        Task DeleteAsync(CouponUsed couponUsed, CancellationToken ct = default);
    }
}
