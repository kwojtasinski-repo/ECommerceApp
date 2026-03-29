using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface IScopeTargetRepository
    {
        Task<IReadOnlyList<CouponScopeTarget>> GetByScopeTypeAndTargetIdAsync(
            string scopeType, int targetId, CancellationToken ct = default);
        Task AddRangeAsync(IReadOnlyList<CouponScopeTarget> targets, CancellationToken ct = default);
        Task UpdateRangeAsync(IReadOnlyList<CouponScopeTarget> targets, CancellationToken ct = default);
    }
}
