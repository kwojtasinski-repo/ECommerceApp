using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Orders
{
    public interface IOrderItemRepository
    {
        Task<OrderItem?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<int> AddAsync(OrderItem item, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<OrderItem>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
        Task AssignToOrderAsync(IReadOnlyList<int> itemIds, int orderId, CancellationToken ct = default);
        Task<IReadOnlyList<OrderItem>> GetCartItemsByUserIdAsync(string userId, CancellationToken ct = default);
        Task<IReadOnlyList<int>> GetCartItemIdsByUserIdAsync(string userId, CancellationToken ct = default);
        Task<IReadOnlyList<OrderItem>> GetAllPagedAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<int> GetAllPagedCountAsync(string? search, CancellationToken ct = default);
        Task<int> GetCartItemCountByUserIdAsync(string userId, CancellationToken ct = default);
    }
}
