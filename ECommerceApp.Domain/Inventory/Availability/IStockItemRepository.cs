using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IStockItemRepository
    {
        Task<StockItem?> GetByProductIdAsync(int productId, CancellationToken ct = default);
        IAsyncEnumerable<StockItem> GetByProductIdsAsync(IReadOnlyList<int> productIds, CancellationToken ct = default);
        Task<StockItem?> GetByIdAsync(StockItemId id, CancellationToken ct = default);
        Task AddAsync(StockItem stockItem, CancellationToken ct = default);
        Task UpdateAsync(StockItem stockItem, CancellationToken ct = default);
        Task<IReadOnlyList<StockItem>> GetAvailableAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<int> GetAvailableCountAsync(string searchString, CancellationToken ct = default);
        Task<IReadOnlyList<StockItem>> GetAllPagedAsync(int page, int pageSize, CancellationToken ct = default);
        Task<int> GetAllCountAsync(CancellationToken ct = default);
    }
}
