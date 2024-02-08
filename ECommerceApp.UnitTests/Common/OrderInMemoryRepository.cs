using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.UnitTests.Common
{
    internal sealed class OrderInMemoryRepository : IOrderRepository
    {
        private readonly GenericInMemoryRepository<Order> _repository = new();

        public int AddOrder(Order order)
        {
            return _repository.Add(order);
        }

        public bool DeleteOrder(int orderId)
        {
            return _repository.Delete(orderId);
        }

        public bool ExistsByCustomerIdAndUserId(int customerId, string userId)
        {
            return _repository.GetAll().Any(o => o.CustomerId == customerId && o.UserId == userId);
        }

        public bool ExistsByIdAndUserId(int id, string userId)
        {
            return _repository.GetAll().Any(o => o.Id == id && o.UserId == userId);
        }

        public List<Order> GetAllOrders()
        {
            return _repository.GetAll().ToList();
        }

        public List<Order> GetAllOrders(int customerId)
        {
            return _repository.GetAll().Where(o => o.CustomerId == customerId).ToList();
        }

        public List<Order> GetAllOrders(string userId)
        {
            return _repository.GetAll().Where(o => o.UserId == userId).ToList();
        }

        public List<Order> GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            return _repository.GetAll().Where(o => o.Number.StartsWith(searchString)).Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
        }

        public List<Order> GetAllOrders(int customerId, int pageSize, int pageNo)
        {
            return _repository.GetAll().Where(o => o.CustomerId == customerId).Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
        }

        public List<Order> GetAllOrders(string userId, int pageSize, int pageNo)
        {
            return _repository.GetAll().Where(o => o.UserId == userId).Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
        }

        public List<Order> GetAllPaidOrders(int pageSize, int pageNo, string searchString)
        {
            return _repository.GetAll().Where(o => o.Number.StartsWith(searchString) && o.IsPaid).Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
        }

        public int GetCountByCustomerId(int customerId)
        {
            return _repository.GetAll().Where(o => o.CustomerId == customerId).Count();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _repository.GetAll()
                           .Where(o => o.Number.StartsWith(searchString))
                           .Count();
        }

        public int GetCountByUserId(string userId)
        {
            return _repository.GetAll()
                           .Where(o => o.UserId == userId)
                           .Count();
        }

        public int GetCountPaidOrdersBySearchString(string searchString)
        {
            return _repository.GetAll()
                           .Where(o => o.IsPaid == true && o.IsDelivered == false)
                           .Where(o => o.Number.StartsWith(searchString))
                           .Count();
        }

        public int GetCustomerFromOrder(int orderId)
        {
            return _repository.GetAll()
                           .Where(o => o.Id == orderId)
                           .Select(or => or.CustomerId)
                           .FirstOrDefault();
        }

        public Order GetOrderById(int id)
        {
            return _repository.GetById(id);
        }

        public Order GetOrderByRefundId(int refundId)
        {
            return _repository.GetAll().FirstOrDefault(o => o.RefundId == refundId);
        }

        public Order GetOrderDetailsById(int id)
        {
            return _repository.GetById(id);
        }

        public Order GetOrderForRealizationById(int id)
        {
            return _repository.GetAll().FirstOrDefault(o => o.Id == id && !o.IsPaid);
        }

        public Order GetOrderPaidAndDeliveredById(int id)
        {
            return _repository.GetAll().FirstOrDefault(o => o.Id == id && o.IsPaid && o.IsDelivered);
        }

        public Order GetOrderPaidAndNotDelivered(int orderId)
        {
            return _repository.GetAll().FirstOrDefault(o => o.Id == orderId && o.IsDelivered == false && o.IsPaid == true);
        }

        public Order GetOrderSummaryById(int orderId)
        {
            return _repository.GetAll().FirstOrDefault(o => o.Id == orderId && !o.IsPaid);
        }

        public void UpdatedOrder(Order order)
        {
            _repository.Update(order);
        }
    }
}
