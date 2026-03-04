using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IProductSnapshotRepository
    {
        Task<ProductSnapshot?> GetByProductIdAsync(int productId, CancellationToken ct = default);
        Task UpsertAsync(ProductSnapshot snapshot, CancellationToken ct = default);
    }
}
