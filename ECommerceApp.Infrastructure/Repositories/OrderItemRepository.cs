using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderItemRepository : IOrderItemRepository
    {
        private readonly Context _context;
        private readonly IGenericRepository<OrderItem> _repository;

        public OrderItemRepository(Context context, IGenericRepository<OrderItem> repository)
        {
            _context = context;
            _repository = repository;
        }

        public int AddOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Add(orderItem);
            _context.SaveChanges();
            return orderItem.Id;
        }

        public void DeleteOrderItem(int orderItemId)
        {
            var orderItem = _context.OrderItem.Find(orderItemId);
            if (orderItem != null)
            {
                _context.OrderItem.Remove(orderItem);
                _context.SaveChanges();
            }
        }

        public IQueryable<OrderItem> GetAllOrderItems()
        {
            var orderItems = _context.OrderItem.AsQueryable();
            return orderItems;
        }

        public OrderItem GetOrderItemById(int orderItemId)
        {
            var orderItem = _context.OrderItem.FirstOrDefault(o => o.Id == orderItemId);
            return orderItem;
        }

        public List<OrderItem> GetOrderItemsToRealization(IEnumerable<int> ids)
        {
            return _context.OrderItem
                .Where(oi => ids.Contains(oi.Id) && oi.OrderId == null)
                .Include(i => i.Item)
                .ToList()
                ?? new List<OrderItem>();
        }

        public void UpdateOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Update(orderItem);
            _context.SaveChanges();
        }

        public void UpdateRange(List<OrderItem> orderItems)
        {
            _repository.UpdateRange(orderItems);
        }
    }
}
