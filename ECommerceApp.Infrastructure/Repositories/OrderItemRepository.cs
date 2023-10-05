using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public void UpdateOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Update(orderItem);
            _context.SaveChanges();
        }
    }
}
