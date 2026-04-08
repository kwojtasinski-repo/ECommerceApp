using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Fulfillment;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment.Repositories
{
    internal sealed class RefundRepository : IRefundRepository
    {
        private readonly IFulfillmentDbContext _context;

        public RefundRepository(IFulfillmentDbContext context)
        {
            _context = context;
        }

        public async Task<Refund?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Refunds
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == new RefundId(id), ct);

        public async Task<Refund?> FindActiveByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.Refunds
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.OrderId == orderId && r.Status == RefundStatus.Requested, ct);

        public async Task<int> AddAsync(Refund refund, CancellationToken ct = default)
        {
            _context.Refunds.Add(refund);
            await _context.SaveChangesAsync(ct);
            return refund.Id.Value;
        }

        public async Task UpdateAsync(Refund refund, CancellationToken ct = default)
        {
            _context.Refunds.Update(refund);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<Refund>> GetPagedAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var query = _context.Refunds
                .AsNoTracking()
                .Include(r => r.Items)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Reason.Contains(search));
            }

            return await query
                .OrderByDescending(r => r.RequestedAt)
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetCountAsync(string? search, CancellationToken ct = default)
        {
            var query = _context.Refunds.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Reason.Contains(search));
            }

            return await query.CountAsync(ct);
        }
    }
}
