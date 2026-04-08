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

        public Task<CouponApplicationRecord?> FindByCouponUsedIdAsync(int couponUsedId, CancellationToken ct = default)
            => _context.CouponApplicationRecords
                .FirstOrDefaultAsync(r => r.CouponUsedId == couponUsedId, ct);

        public async Task<IReadOnlyList<CouponApplicationRecord>> FindByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.CouponApplicationRecords
                .Join(_context.CouponUsed,
                    record => record.CouponUsedId,
                    used => used.Id.Value,
                    (record, used) => new { record, used })
                .Where(x => x.used.OrderId == orderId)
                .Select(x => x.record)
                .ToListAsync(ct);

        public async Task UpdateAsync(CouponApplicationRecord record, CancellationToken ct = default)
        {
            _context.CouponApplicationRecords.Update(record);
            await _context.SaveChangesAsync(ct);
        }
    }
}
