using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class StockSnapshotRepository : IStockSnapshotRepository
    {
        private readonly PresaleDbContext _context;

        public StockSnapshotRepository(PresaleDbContext context)
        {
            _context = context;
        }

        public async Task<StockSnapshot?> FindByProductIdAsync(PresaleProductId productId, CancellationToken ct = default)
            => await _context.StockSnapshots
                .FirstOrDefaultAsync(s => s.ProductId == productId, ct);

        public async Task AddAsync(StockSnapshot snapshot, CancellationToken ct = default)
        {
            _context.StockSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(StockSnapshot snapshot, CancellationToken ct = default)
        {
            _context.StockSnapshots.Update(snapshot);
            await _context.SaveChangesAsync(ct);
        }
    }
}
