using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponUsedRepository
    {
        Task<CouponUsed> FindByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task AddAsync(CouponUsed couponUsed, CancellationToken ct = default);
        Task DeleteAsync(CouponUsed couponUsed, CancellationToken ct = default);

        // ── Slice 2 additions ────────────────────────────────────────────
        Task<IReadOnlyList<CouponUsed>> FindAllByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<int> CountByUserAndCouponAsync(string userId, int couponId, CancellationToken ct = default);
    }
}
