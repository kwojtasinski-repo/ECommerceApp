using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IStockAuditRepository
    {
        Task AddAsync(StockAuditEntry entry, CancellationToken ct = default);
        Task<(IReadOnlyList<StockAuditEntry> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    }
}
