using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IStockHoldRepository
    {
        Task<StockHold?> GetByOrderAndProductAsync(int orderId, int productId, CancellationToken ct = default);
        Task<IReadOnlyList<StockHold>> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<IReadOnlyList<StockHold>> GetPagedAsync(int page, int pageSize, StockHoldStatus[]? statuses, CancellationToken ct = default);
        Task<int> GetCountAsync(StockHoldStatus[]? statuses, CancellationToken ct = default);
        Task AddAsync(StockHold stockHold, CancellationToken ct = default);
        Task UpdateAsync(StockHold stockHold, CancellationToken ct = default);
        Task DeleteAsync(StockHold stockHold, CancellationToken ct = default);
    }
}
