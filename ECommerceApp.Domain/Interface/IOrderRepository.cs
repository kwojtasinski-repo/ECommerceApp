using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        void DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        IQueryable<Order> GetAllOrders();
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdatedOrder(Order order);
        Order GetByIdReadOnly(int id);
    }
}
