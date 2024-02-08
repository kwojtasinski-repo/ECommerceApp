using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.UnitTests.Common
{
    internal sealed class OrderItemInMemoryRepository : IOrderItemRepository
    {
        private readonly GenericInMemoryRepository<OrderItem> _repository = new ();

        public int AddOrderItem(OrderItem orderItem)
        {
            return _repository.Add(orderItem);
        }

        public bool DeleteOrderItem(int orderItemId)
        {
            return _repository.Delete(orderItemId);
        }

        public bool ExistsById(int id)
        {
            return _repository.GetAll().Any(oi => oi.Id == id);
        }

        public List<OrderItem> GetAllOrderItems()
        {
            return _repository.GetAll().ToList();
        }

        public int GetCountByItemId(int itemId)
        {
            return _repository.GetAll().Where(oi => oi.ItemId == itemId).Count();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _repository.GetAll()
                    .Where(oi => oi.Item is not null && oi.Item.Name.StartsWith(searchString) 
                            || oi.Item?.Brand is not null && oi.Item.Brand.Name.StartsWith(searchString)
                            || oi.Item?.Type is not null && oi.Item.Type.Name.StartsWith(searchString))
                    .Count();
        }

        public int GetCountNotOrderedByUserId(string userId)
        {
            return _repository.GetAll()
                              .Where(oi => oi.UserId == userId && oi.OrderId == null)
                              .Count();
        }

        public OrderItem GetOrderItemById(int orderItemId)
        {
            return _repository.GetById(orderItemId);
        }

        public OrderItem GetOrderItemDetailsById(int orderItemId)
        {
            return _repository.GetById(orderItemId);
        }

        public List<OrderItem> GetOrderItems(string searchString, int pageSize, int pageNo)
        {
            return _repository.GetAll().ToList();
        }

        public List<OrderItem> GetOrderItemsByItemId(int itemId)
        {
            return _repository.GetAll()
                       .Where(oi => oi.ItemId == itemId)
                       .ToList();
        }

        public List<OrderItem> GetOrderItemsByItemId(int itemId, int pageSize, int pageNo)
        {
            return _repository.GetAll()
                       .Where(oi => oi.ItemId == itemId)
                       .Skip(pageSize * (pageNo - 1))
                       .Take(pageSize)
                       .ToList();
        }

        public List<OrderItem> GetOrderItemsToRealization(IEnumerable<int> ids)
        {
            return _repository.GetAll()
                              .Where(oi => ids.Contains(oi.Id) && oi.OrderId == null)
                              .ToList();
        }

        public OrderItem GetUserOrderItemNotOrdered(string userId, int itemId)
        {
            return _repository.GetAll().FirstOrDefault(oi => oi.ItemId == itemId && oi.UserId == userId && oi.OrderId == null);
        }

        public List<int> GetUserOrderItemsId(string userId)
        {
            return _repository.GetAll()
                    .Where(oi => oi.UserId == userId && oi.OrderId == null)
                    .Select(oi => oi.Id)
                    .ToList();
        }

        public List<OrderItem> GetUserOrderItemsNotOrdered(string userId)
        {
            return _repository.GetAll().Where(oi => oi.UserId == userId && oi.OrderId == null).ToList();
        }

        public List<OrderItem> GetUserOrderItemsNotOrdered(string userId, int pageSize, int pageNo)
        {
            return _repository.GetAll()
                              .Where(oi => oi.UserId == userId && oi.OrderId == null)
                              .Skip(pageSize * (pageNo - 1))
                              .Take(pageSize)
                              .ToList();
        }

        public void UpdateOrderItem(OrderItem orderItem)
        {
            _repository.Update(orderItem);
        }

        public void UpdateRange(List<OrderItem> orderItems)
        {
            orderItems.ForEach(oi => _repository.Update(oi));
        }
    }
}
