using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public interface IRefundRepository
    {
        Task<Refund?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Refund?> FindActiveByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<int> AddAsync(Refund refund, CancellationToken ct = default);
        Task UpdateAsync(Refund refund, CancellationToken ct = default);
        Task<IReadOnlyList<Refund>> GetPagedAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<int> GetCountAsync(string? search, CancellationToken ct = default);
        Task<IReadOnlyList<Refund>> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
    }
}
