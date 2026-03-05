using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class StockItemRepository : IStockItemRepository
    {
        private readonly AvailabilityDbContext _context;

        public StockItemRepository(AvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task<StockItem?> GetByProductIdAsync(int productId, CancellationToken ct = default)
            => await _context.StockItems
                .FirstOrDefaultAsync(s => s.ProductId == new StockProductId(productId), ct);

        public async Task<StockItem?> GetByIdAsync(StockItemId id, CancellationToken ct = default)
            => await _context.StockItems
                .FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task AddAsync(StockItem stockItem, CancellationToken ct = default)
        {
            _context.StockItems.Add(stockItem);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(StockItem stockItem, CancellationToken ct = default)
        {
            _context.StockItems.Update(stockItem);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<StockItem>> GetAvailableAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var query = _context.StockItems.AsNoTracking()
                .Where(s => s.Quantity.Value - s.ReservedQuantity.Value > 0);

            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(s => s.ProductId.Value.ToString().Contains(searchString));

            return await query
                .OrderBy(s => s.ProductId)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetAvailableCountAsync(string searchString, CancellationToken ct = default)
        {
            var query = _context.StockItems.AsNoTracking()
                .Where(s => s.Quantity.Value - s.ReservedQuantity.Value > 0);

            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(s => s.ProductId.Value.ToString().Contains(searchString));

            return await query.CountAsync(ct);
        }
    }
}
