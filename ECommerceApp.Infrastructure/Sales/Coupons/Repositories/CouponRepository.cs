using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Repositories
{
    internal sealed class CouponRepository : ICouponRepository
    {
        private readonly ICouponsDbContext _context;

        public CouponRepository(ICouponsDbContext context)
        {
            _context = context;
        }

        public Task<Coupon> GetByCodeAsync(string code, CancellationToken ct = default)
            => _context.Coupons.FirstOrDefaultAsync(c => c.Code.Value == code, ct);

        public Task<Coupon> GetByIdAsync(int id, CancellationToken ct = default)
            => _context.Coupons.FirstOrDefaultAsync(c => c.Id == new CouponId(id), ct);

        public async Task<IReadOnlyList<Coupon>> GetAllAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var query = _context.Coupons.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c => c.Code.Value.Contains(searchString) || c.Description.Value.Contains(searchString));
            }

            return await query.OrderBy(c => c.Code.Value).Skip((pageNo - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        }

        public async Task<int> CountAsync(string searchString, CancellationToken ct = default)
        {
            var query = _context.Coupons.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c => c.Code.Value.Contains(searchString) || c.Description.Value.Contains(searchString));
            }

            return await query.CountAsync(ct);
        }

        public async Task UpdateAsync(Coupon coupon, CancellationToken ct = default)
        {
            _context.Coupons.Update(coupon);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateWithVersionAsync(Coupon coupon, byte[] originalVersion, CancellationToken ct = default)
        {
            _context.Coupons.Update(coupon);

            // The tracked entity already carries the version just read from the database, so EF would
            // compare the row against itself and never detect anything. Point the concurrency check at
            // the version the edit actually started from.
            if (originalVersion is not null)
            {
                _context.Entry(coupon).Property(c => c.Version).OriginalValue = originalVersion;
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task AddAsync(Coupon coupon, CancellationToken ct = default)
        {
            await _context.Coupons.AddAsync(coupon, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Coupon coupon, CancellationToken ct = default)
        {
            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync(ct);
        }
    }
}
