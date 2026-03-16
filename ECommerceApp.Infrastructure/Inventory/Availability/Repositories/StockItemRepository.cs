using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public async IAsyncEnumerable<StockItem> GetByProductIdsAsync(
            IReadOnlyList<int> productIds,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (productIds.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < productIds.Count; i += BatchSize)
            {
                var batch = productIds.Skip(i).Take(BatchSize)
                            .Select(id => new StockProductId(id))
                            .ToList();
                await foreach (var item in _context.StockItems
                    .AsNoTracking()
                    .Where(s => batch.Contains(s.ProductId))
                    .AsAsyncEnumerable()
                    .WithCancellation(ct))
                {
                    yield return item;
                }
            }
        }

        private const int BatchSize = 200;

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

        public async Task<IReadOnlyList<StockItem>> GetAllPagedAsync(
            int page, int pageSize, CancellationToken ct = default)
            => await _context.StockItems
                .AsNoTracking()
                .OrderBy(s => s.ProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public async Task<int> GetAllCountAsync(CancellationToken ct = default)
            => await _context.StockItems.AsNoTracking().CountAsync(ct);
    }
}
