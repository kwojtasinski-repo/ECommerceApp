using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public interface IStockSnapshotRepository
    {
        Task<StockSnapshot?> FindByProductIdAsync(PresaleProductId productId, CancellationToken ct = default);
        Task AddAsync(StockSnapshot snapshot, CancellationToken ct = default);
        Task UpdateAsync(StockSnapshot snapshot, CancellationToken ct = default);
    }
}
