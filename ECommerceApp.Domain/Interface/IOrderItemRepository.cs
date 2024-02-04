using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderItemRepository
    {
        bool DeleteOrderItem(int orderItemId);
        int AddOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemById(int orderItemId);
        OrderItem GetOrderItemDetailsById(int orderItemId);
        OrderItem GetUserOrderItemNotOrdered(string userId, int itemId);
        List<OrderItem> GetAllOrderItems();
        List<OrderItem> GetOrderItems(string searchString, int pageSize, int pageNo);
        List<OrderItem> GetOrderItemsByItemId(int itemId);
        List<OrderItem> GetOrderItemsByItemId(int itemId, int pageSize, int pageNo);
        List<OrderItem> GetUserOrderItemsNotOrdered(string userId);
        List<OrderItem> GetUserOrderItemsNotOrdered(string userId, int pageSize, int pageNo);
        List<int> GetUserOrderItemsId(string userId);
        void UpdateOrderItem(OrderItem orderItem);
        List<OrderItem> GetOrderItemsToRealization(IEnumerable<int> ids);
        void UpdateRange(List<OrderItem> orderItems);
        int GetCountNotOrderedByUserId(string userId);
        int GetCountByItemId(int itemId);
        bool ExistsById(int id);
        int GetCountBySearchString(string searchString);
    }
}
