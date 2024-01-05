using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderItemRepository : IGenericRepository<OrderItem>
    {
        void DeleteOrderItem(int orderItemId);
        int AddOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemById(int orderItemId);
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdateOrderItem(OrderItem orderItem);
        List<OrderItem> GetOrderItems(List<int> ids);
    }
}
