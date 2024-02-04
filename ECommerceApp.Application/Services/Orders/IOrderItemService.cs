using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.OrderItem;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Orders
{
    public interface IOrderItemService
    {
        int AddOrderItem(OrderItemDto model);
        OrderItemDto GetOrderItemDetails(int id);
        bool UpdateOrderItem(OrderItemDto model);
        IEnumerable<OrderItemDto> GetOrderItems();
        IEnumerable<OrderItemDto> GetOrderItemsByItemId(int itemId);
        ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString);
        bool OrderItemExists(int id);
        bool DeleteOrderItem(int id);
        void UpdateOrderItems(IEnumerable<OrderItemDto> orderItems);
        IEnumerable<OrderItemDto> GetOrderItemsForRealization(string userId);
        IEnumerable<int> GetOrderItemsIdsForRealization(string userId);
        int OrderItemCount(string userId);
        int AddOrderItem(int id, string userId);
        ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        List<OrderItemDto> GetOrderItemsNotOrdered(IEnumerable<int> ids);
    }
}
