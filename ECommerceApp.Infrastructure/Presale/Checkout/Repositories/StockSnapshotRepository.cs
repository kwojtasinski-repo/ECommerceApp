using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class StockSnapshotRepository : IStockSnapshotRepository
    {
        private readonly IPresaleDbContext _context;
        private const int BatchSize = 200;

        public StockSnapshotRepository(IPresaleDbContext context)
        {
            _context = context;
        }

        public async Task<StockSnapshot?> FindByProductIdAsync(PresaleProductId productId, CancellationToken ct = default)
            => await _context.StockSnapshots
                .FirstOrDefaultAsync(s => s.ProductId == productId, ct);

        public async IAsyncEnumerable<StockSnapshot> GetByProductIdsAsync(
            IReadOnlyList<int> productIds,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (productIds.Count == 0)
                yield break;

            for (int i = 0; i < productIds.Count; i += BatchSize)
            {
                var batch = productIds.Skip(i).Take(BatchSize)
                    .Select(id => new PresaleProductId(id))
                    .ToList();
                await foreach (var snapshot in _context.StockSnapshots
                    .AsNoTracking()
                    .Where(s => batch.Contains(s.ProductId))
                    .AsAsyncEnumerable()
                    .WithCancellation(ct))
                {
                    yield return snapshot;
                }
            }
        }

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
