using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderItemRepository : IGenericRepository<OrderItem>
    {
        void DeleteOrderItem(int orderItemId);
        int AddOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemById(int orderItemId);
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdateOrderItem(OrderItem orderItem);
    }
}
