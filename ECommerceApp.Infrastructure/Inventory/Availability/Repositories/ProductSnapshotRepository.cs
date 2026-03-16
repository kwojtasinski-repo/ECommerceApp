using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class ProductSnapshotRepository : IProductSnapshotRepository
    {
        private readonly AvailabilityDbContext _context;

        public ProductSnapshotRepository(AvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task<ProductSnapshot?> GetByProductIdAsync(int productId, CancellationToken ct = default)
            => await _context.ProductSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == productId, ct);

        public async Task<IReadOnlyList<ProductSnapshot>> GetByProductIdsAsync(
            IReadOnlyList<int> productIds, CancellationToken ct = default)
            => await _context.ProductSnapshots
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync(ct);

        public async Task UpsertAsync(ProductSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.ProductSnapshots
                .FirstOrDefaultAsync(p => p.ProductId == snapshot.ProductId, ct);

            if (existing is null)
            {
                _context.ProductSnapshots.Add(snapshot);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(snapshot);
            }

            await _context.SaveChangesAsync(ct);
        }
    }
}
