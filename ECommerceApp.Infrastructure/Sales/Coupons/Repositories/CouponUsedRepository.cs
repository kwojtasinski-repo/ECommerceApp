using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Repositories
{
    internal sealed class CouponUsedRepository : ICouponUsedRepository
    {
        private readonly CouponsDbContext _context;

        public CouponUsedRepository(CouponsDbContext context)
        {
            _context = context;
        }

        public Task<CouponUsed?> FindByOrderIdAsync(int orderId, CancellationToken ct = default)
            => _context.CouponUsed.FirstOrDefaultAsync(cu => cu.OrderId == orderId, ct);

        public async Task AddAsync(CouponUsed couponUsed, CancellationToken ct = default)
        {
            await _context.CouponUsed.AddAsync(couponUsed, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(CouponUsed couponUsed, CancellationToken ct = default)
        {
            _context.CouponUsed.Remove(couponUsed);
            await _context.SaveChangesAsync(ct);
        }
    }
}
