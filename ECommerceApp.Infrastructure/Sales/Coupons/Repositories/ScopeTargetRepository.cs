using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Repositories
{
    internal sealed class ScopeTargetRepository : IScopeTargetRepository
    {
        private readonly CouponsDbContext _db;

        public ScopeTargetRepository(CouponsDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<CouponScopeTarget>> GetByScopeTypeAndTargetIdAsync(
            string scopeType, int targetId, CancellationToken ct = default)
        {
            return await _db.Set<CouponScopeTarget>()
                .Where(t => t.ScopeType.Value == scopeType && t.TargetId == targetId)
                .ToListAsync(ct);
        }

        public async Task AddRangeAsync(IReadOnlyList<CouponScopeTarget> targets, CancellationToken ct = default)
        {
            await _db.Set<CouponScopeTarget>().AddRangeAsync(targets, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateRangeAsync(IReadOnlyList<CouponScopeTarget> targets, CancellationToken ct = default)
        {
            _db.Set<CouponScopeTarget>().UpdateRange(targets);
            await _db.SaveChangesAsync(ct);
        }
    }
}
