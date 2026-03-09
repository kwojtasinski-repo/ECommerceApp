using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Orders
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Order?> GetByIdWithItemsAsync(int id, CancellationToken ct = default);
        Task<Order?> GetByRefundIdWithItemsAsync(int refundId, CancellationToken ct = default);
        Task<int> AddAsync(Order order, CancellationToken ct = default);
        Task UpdateAsync(Order order, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<Order>> GetAllAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<int> GetAllCountAsync(string? search, CancellationToken ct = default);
        Task<IReadOnlyList<Order>> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task<IReadOnlyList<Order>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default);
        Task<IReadOnlyList<Order>> GetAllPaidAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<int> GetAllPaidCountAsync(string? search, CancellationToken ct = default);
        Task<int?> GetCustomerIdAsync(int orderId, CancellationToken ct = default);
    }
}
