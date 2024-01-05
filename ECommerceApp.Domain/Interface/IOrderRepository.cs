using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        bool DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        IQueryable<Order> GetAllOrders();
        IQueryable<OrderItem> GetAllOrderItems();
        void UpdatedOrder(Order order);
        Order GetByIdReadOnly(int id);
        Order GetOrderForRealizationById(int id);
    }
}
