using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Repositories
{
    internal sealed class CouponApplicationRecordRepository : ICouponApplicationRecordRepository
    {
        private readonly ICouponsDbContext _context;

        public CouponApplicationRecordRepository(ICouponsDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(CouponApplicationRecord record, CancellationToken ct = default)
        {
            await _context.CouponApplicationRecords.AddAsync(record, ct);
            await _context.SaveChangesAsync(ct);
        }

        public Task<CouponApplicationRecord> FindByCouponUsedIdAsync(int couponUsedId, CancellationToken ct = default)
            => _context.CouponApplicationRecords
                .FirstOrDefaultAsync(r => r.CouponUsedId == couponUsedId, ct);

        public async Task<IReadOnlyList<CouponApplicationRecord>> FindByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            // Two queries on purpose: CouponUsed.Id is a TypedId (value converter) and CouponUsedId is a plain int
            // with no FK, so a Join on used.Id.Value cannot be translated to SQL Server. The typed ids are
            // materialized first and unwrapped (.Value) in memory.
            var usedIds = await _context.CouponUsed
                .Where(used => used.OrderId == orderId)
                .Select(used => used.Id)
                .ToListAsync(ct);

            var rawUsedIds = usedIds.Select(id => id.Value).ToList();

            // SQL (EF Core, SQL Server):
            //   -- 1st query: SELECT [c].[Id] FROM [sales].[CouponUsed] AS [c] WHERE [c].[OrderId] = @orderId
            //   -- 2nd query:
            //   WHERE [c].[CouponUsedId] IN (@rawUsedIds1, @rawUsedIds2, ...)
            // Why: the ids are plain ints, so the IN list translates; the TypedId .Value join did not.
            return await _context.CouponApplicationRecords
                .Where(record => rawUsedIds.Contains(record.CouponUsedId))
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(CouponApplicationRecord record, CancellationToken ct = default)
        {
            _context.CouponApplicationRecords.Update(record);
            await _context.SaveChangesAsync(ct);
        }
    }
}
