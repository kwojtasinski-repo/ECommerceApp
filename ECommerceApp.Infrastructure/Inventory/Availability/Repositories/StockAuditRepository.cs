using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class StockAuditRepository : IStockAuditRepository
    {
        private readonly IAvailabilityDbContext _context;

        public StockAuditRepository(IAvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(StockAuditEntry entry, CancellationToken ct = default)
        {
            _context.StockAuditEntries.Add(entry);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<(IReadOnlyList<StockAuditEntry> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, CancellationToken ct = default)
        {
            var query = _context.StockAuditEntries.AsNoTracking();
            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(e => e.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return (items, total);
        }
    }
}
