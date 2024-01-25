using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderItemRepository
    {
        void DeleteOrderItem(int orderItemId);
        int AddOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemById(int orderItemId);
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdateOrderItem(OrderItem orderItem);
        List<OrderItem> GetOrderItemsToRealization(IEnumerable<int> ids);
        void UpdateRange(List<OrderItem> orderItems);
    }
}
