using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    public interface IOrderItemService
    {
        Task<int> AddCartItemAsync(AddOrderItemDto dto, CancellationToken ct = default);
        Task<OrderOperationResult> DeleteCartItemAsync(int itemId, CancellationToken ct = default);
        Task<OrderItemVm> GetByIdAsync(int itemId, CancellationToken ct = default);
        Task<IReadOnlyList<OrderItemForListVm>> GetCartItemsByUserIdAsync(string userId, CancellationToken ct = default);
        Task<IReadOnlyList<int>> GetCartItemIdsByUserIdAsync(string userId, CancellationToken ct = default);
        Task<OrderItemListVm> GetAllPagedAsync(int pageSize, int pageNo, string search, CancellationToken ct = default);
        Task<int> GetCartItemCountByUserIdAsync(string userId, CancellationToken ct = default);
    }
}
