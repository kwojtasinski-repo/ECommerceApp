using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using System;
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
                .FirstOrDefaultAsync(p => p.ProductId == productId, ct);

        public async Task UpsertAsync(int productId, int newQuantity, CancellationToken ct = default)
        {
            var existing = await _context.PendingStockAdjustments
                .FirstOrDefaultAsync(p => p.ProductId == productId, ct);

            if (existing != null)
            {
                _context.PendingStockAdjustments.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }

            var record = PendingStockAdjustment.Create(productId, newQuantity);
            _context.PendingStockAdjustments.Add(record);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteIfVersionMatchesAsync(int productId, Guid version, CancellationToken ct = default)
        {
            var existing = await _context.PendingStockAdjustments
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.Version == version, ct);

            if (existing != null)
            {
                _context.PendingStockAdjustments.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
