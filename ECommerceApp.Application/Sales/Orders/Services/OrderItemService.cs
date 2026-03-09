using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    internal sealed class OrderItemService : IOrderItemService
    {
        private readonly IOrderItemRepository _repo;

        public OrderItemService(IOrderItemRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> AddCartItemAsync(AddOrderItemDto dto, CancellationToken ct = default)
        {
            var item = OrderItem.Create(dto.ItemId, dto.Quantity, dto.UnitCost, dto.UserId);
            return await _repo.AddAsync(item, ct);
        }

        public async Task<OrderOperationResult> DeleteCartItemAsync(int itemId, CancellationToken ct = default)
        {
            var item = await _repo.GetByIdAsync(itemId, ct);
            if (item is null)
                return OrderOperationResult.OrderNotFound;

            await _repo.DeleteAsync(itemId, ct);
            return OrderOperationResult.Success;
        }

        public async Task<OrderItemVm?> GetByIdAsync(int itemId, CancellationToken ct = default)
        {
            var item = await _repo.GetByIdAsync(itemId, ct);
            return item is null ? null : MapToVm(item);
        }

        public async Task<IReadOnlyList<OrderItemForListVm>> GetCartItemsByUserIdAsync(string userId, CancellationToken ct = default)
        {
            var items = await _repo.GetCartItemsByUserIdAsync(userId, ct);
            return items.Select(MapToForListVm).ToList();
        }

        public Task<IReadOnlyList<int>> GetCartItemIdsByUserIdAsync(string userId, CancellationToken ct = default)
            => _repo.GetCartItemIdsByUserIdAsync(userId, ct);

        public async Task<OrderItemListVm> GetAllPagedAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var items = await _repo.GetAllPagedAsync(pageSize, pageNo, search, ct);
            var count = await _repo.GetAllPagedCountAsync(search, ct);
            return new OrderItemListVm
            {
                Items = items.Select(MapToForListVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = count,
                SearchString = search
            };
        }

        public Task<int> GetCartItemCountByUserIdAsync(string userId, CancellationToken ct = default)
            => _repo.GetCartItemCountByUserIdAsync(userId, ct);

        private static OrderItemVm MapToVm(OrderItem item)
            => new()
            {
                Id = item.Id.Value,
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                CouponUsedId = item.CouponUsedId,
                RefundId = item.RefundId
            };

        private static OrderItemForListVm MapToForListVm(OrderItem item)
            => new()
            {
                Id = item.Id.Value,
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                UserId = item.UserId,
                OrderId = item.OrderId
            };
    }
}
