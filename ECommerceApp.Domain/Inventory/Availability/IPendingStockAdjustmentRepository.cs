using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IPendingStockAdjustmentRepository
    {
        Task<PendingStockAdjustment> GetByProductIdAsync(int productId, CancellationToken ct = default);
        Task<IReadOnlyList<PendingStockAdjustment>> GetByProductIdsAsync(IReadOnlyList<int> productIds, CancellationToken ct = default);
        Task<IReadOnlyList<PendingStockAdjustment>> GetAllAsync(CancellationToken ct = default);
        Task UpsertAsync(int productId, int newQuantity, CancellationToken ct = default);
        Task DeleteIfVersionMatchesAsync(int productId, Guid version, CancellationToken ct = default);
    }
}
