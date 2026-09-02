using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponRepository
    {
        Task<Coupon> GetByCodeAsync(string code, CancellationToken ct = default);
        Task<Coupon> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<Coupon>> GetAllAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<int> CountAsync(string searchString, CancellationToken ct = default);
        Task UpdateAsync(Coupon coupon, CancellationToken ct = default);

        /// <summary>
        /// Updates the coupon guarding against the rowversion the caller last saw, so a concurrent
        /// write is rejected by the database rather than silently overwritten. Passing the freshly
        /// loaded entity's own version would defeat the check — supply the value the edit started from.
        /// Throws <c>DbUpdateConcurrencyException</c> when the stored version has moved on.
        /// </summary>
        Task UpdateWithVersionAsync(Coupon coupon, byte[] originalVersion, CancellationToken ct = default);
        Task AddAsync(Coupon coupon, CancellationToken ct = default);
        Task DeleteAsync(Coupon coupon, CancellationToken ct = default);
    }
}
