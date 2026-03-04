using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
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

        public async Task UpsertAsync(ProductSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.ProductSnapshots
                .FirstOrDefaultAsync(p => p.ProductId == snapshot.ProductId, ct);

            if (existing != null)
            {
                _context.ProductSnapshots.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }

            _context.ProductSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);
        }
    }
}
