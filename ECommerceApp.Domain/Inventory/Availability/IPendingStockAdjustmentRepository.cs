using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IPendingStockAdjustmentRepository
    {
        Task<PendingStockAdjustment?> GetByProductIdAsync(int productId, CancellationToken ct = default);
        Task UpsertAsync(int productId, int newQuantity, CancellationToken ct = default);
        Task DeleteIfVersionMatchesAsync(int productId, Guid version, CancellationToken ct = default);
    }
}
