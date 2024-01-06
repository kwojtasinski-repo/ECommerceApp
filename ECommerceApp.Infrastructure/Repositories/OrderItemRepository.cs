using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderItemRepository : GenericRepository<OrderItem>, IOrderItemRepository
    {
        public OrderItemRepository(Context context) : base(context)
        {
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

        public List<OrderItem> GetOrderItems(IEnumerable<int> ids)
        {
            return GetDbSet()
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
    }
}
