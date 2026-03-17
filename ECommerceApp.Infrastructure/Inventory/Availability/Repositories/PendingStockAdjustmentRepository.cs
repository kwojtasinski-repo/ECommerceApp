using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class PendingStockAdjustmentRepository : IPendingStockAdjustmentRepository
    {
        private readonly AvailabilityDbContext _context;

        public PendingStockAdjustmentRepository(AvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task<PendingStockAdjustment?> GetByProductIdAsync(int productId, CancellationToken ct = default)
            => await _context.PendingStockAdjustments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == new StockProductId(productId), ct);

        public async Task<IReadOnlyList<PendingStockAdjustment>> GetByProductIdsAsync(
            IReadOnlyList<int> productIds, CancellationToken ct = default)
            => await _context.PendingStockAdjustments
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductId.Value))
                .ToListAsync(ct);

        public async Task<IReadOnlyList<PendingStockAdjustment>> GetAllAsync(CancellationToken ct = default)
            => await _context.PendingStockAdjustments
                .AsNoTracking()
                .OrderBy(p => p.SubmittedAt)
                .ToListAsync(ct);

        public async Task UpsertAsync(int productId, int newQuantity, CancellationToken ct = default)
        {
            var existing = await _context.PendingStockAdjustments
                .FirstOrDefaultAsync(p => p.ProductId == new StockProductId(productId), ct);

            if (existing != null)
            {
                _context.PendingStockAdjustments.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }

            var record = PendingStockAdjustment.Create(new StockProductId(productId), new StockQuantity(newQuantity));
            _context.PendingStockAdjustments.Add(record);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteIfVersionMatchesAsync(int productId, Guid version, CancellationToken ct = default)
        {
            var existing = await _context.PendingStockAdjustments
                .FirstOrDefaultAsync(p => p.ProductId == new StockProductId(productId) && p.Version == version, ct);

            if (existing != null)
            {
                _context.PendingStockAdjustments.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
