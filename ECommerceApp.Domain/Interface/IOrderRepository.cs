using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository
    {
        bool DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        Order GetOrderByRefundId(int refundId);
        List<Order> GetAllOrders();
        List<Order> GetAllOrders(int customerId);
        List<Order> GetAllOrders(string userId);
        List<Order> GetAllOrders(int pageSize, int pageNo, string searchString);
        List<Order> GetAllOrders(int customerId, int pageSize, int pageNo);
        List<Order> GetAllOrders(string userId, int pageSize, int pageNo);
        void UpdatedOrder(Order order);
        Order GetOrderForRealizationById(int id);
        Order GetOrderSummaryById(int orderId);
        Order GetOrderDetailsById(int id);
        Order GetOrderPaidAndDeliveredById(int id);
        int GetCountBySearchString(string searchString);
        int GetCountByCustomerId(int customerId);
        int GetCountByUserId(string userId);
        List<Order> GetAllPaidOrders(int pageSize, int pageNo, string searchString);
        int GetCountPaidOrdersBySearchString(string searchString);
        bool ExistsByIdAndUserId(int id, string userId);
        bool ExistsByCustomerIdAndUserId(int customerId, string userId);
        Order GetOrderPaidAndNotDelivered(int orderId);
        int GetCustomerFromOrder(int orderId);
    }
}
