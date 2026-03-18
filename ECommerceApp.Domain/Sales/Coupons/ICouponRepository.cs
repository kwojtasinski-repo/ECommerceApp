using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponRepository
    {
        Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default);
        Task<Coupon?> GetByIdAsync(int id, CancellationToken ct = default);
        Task UpdateAsync(Coupon coupon, CancellationToken ct = default);

        // ── Slice 2 addition ─────────────────────────────────────────────
        Task AddAsync(Coupon coupon, CancellationToken ct = default);
    }
}
