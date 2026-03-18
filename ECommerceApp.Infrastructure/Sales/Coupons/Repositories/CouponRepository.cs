using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Repositories
{
    internal sealed class CouponRepository : ICouponRepository
    {
        private readonly CouponsDbContext _context;

        public CouponRepository(CouponsDbContext context)
        {
            _context = context;
        }

        public Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
            => _context.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);

        public Task<Coupon?> GetByIdAsync(int id, CancellationToken ct = default)
            => _context.Coupons.FirstOrDefaultAsync(c => c.Id == new CouponId(id), ct);

        public async Task UpdateAsync(Coupon coupon, CancellationToken ct = default)
        {
            _context.Coupons.Update(coupon);
            await _context.SaveChangesAsync(ct);
        }

        public async Task AddAsync(Coupon coupon, CancellationToken ct = default)
        {
            await _context.Coupons.AddAsync(coupon, ct);
            await _context.SaveChangesAsync(ct);
        }
    }
}
