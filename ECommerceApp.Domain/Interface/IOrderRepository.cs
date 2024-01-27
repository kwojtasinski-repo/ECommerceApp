using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository
    {
        bool DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        IQueryable<Order> GetAllOrders();
        List<Order> GetAllOrders(int pageSize, int pageNo, string searchString);
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdatedOrder(Order order);
        Order GetByIdReadOnly(int id);
        Order GetOrderForRealizationById(int id);
        Order GetOrderSummaryById(int orderId);
        Order GetOrderDetailsById(int id);
        int GetCountBySearchString(string searchString);
    }
}
