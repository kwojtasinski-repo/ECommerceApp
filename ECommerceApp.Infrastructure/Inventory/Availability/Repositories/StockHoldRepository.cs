using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class StockHoldRepository : IStockHoldRepository
    {
        private readonly IAvailabilityDbContext _context;

        public StockHoldRepository(IAvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task<StockHold> GetByOrderAndProductAsync(int orderId, int productId, CancellationToken ct = default)
            => await _context.StockHolds
                .FirstOrDefaultAsync(r => r.OrderId == new ReservationOrderId(orderId) && r.ProductId == new StockProductId(productId), ct);

        public async Task<IReadOnlyList<StockHold>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.StockHolds
                .AsNoTracking()
                .Where(r => r.OrderId == new ReservationOrderId(orderId))
                .ToListAsync(ct);

        public async Task<IReadOnlyList<StockHold>> GetPagedAsync(
            int page, int pageSize, StockHoldStatus[] statuses, CancellationToken ct = default)
        {
            var query = _context.StockHolds.AsNoTracking();
            if (statuses != null)
                query = query.Where(h => statuses.Contains(h.Status));
            return await query
                .OrderByDescending(h => h.ReservedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetCountAsync(StockHoldStatus[] statuses, CancellationToken ct = default)
        {
            var query = _context.StockHolds.AsNoTracking();
            if (statuses != null)
                query = query.Where(h => statuses.Contains(h.Status));
            return await query.CountAsync(ct);
        }

        public async Task AddAsync(StockHold stockHold, CancellationToken ct = default)
        {
            _context.StockHolds.Add(stockHold);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(StockHold stockHold, CancellationToken ct = default)
        {
            _context.StockHolds.Update(stockHold);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(StockHold stockHold, CancellationToken ct = default)
        {
            _context.StockHolds.Remove(stockHold);
            await _context.SaveChangesAsync(ct);
        }
    }
}
