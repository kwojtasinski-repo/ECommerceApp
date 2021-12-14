using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderRepository : GenericRepository<Order>,  IOrderRepository
    {
        public OrderRepository(Context context) : base(context)
        {
        }

        public void DeleteOrder(int orderId)
        {
            var order = _context.Orders
                .Include(inc => inc.CouponUsed)
                .Include(inc => inc.Customer)
                .Include(inc => inc.Payment)
                .Include(inc => inc.Refund)
                .FirstOrDefault(o => o.Id == orderId);

            if (order != null)
            {
                var orderWithItems = _context.Orders.Include(p => p.OrderItems)
                                .SingleOrDefault(p => p.Id == orderId);

                foreach (var orderItem in orderWithItems.OrderItems.ToList())
                {
                    _context.OrderItem.Remove(orderItem);
                }
                _context.Orders.Remove(order);
                _context.SaveChanges();
            }
        }

        public int AddOrder(Order order)
        {
            var orderItems = order.OrderItems.Select(oi => new OrderItem { Id = oi.Id, CouponUsedId = oi.CouponUsedId, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity, OrderId = oi.OrderId, RefundId = oi.RefundId, UserId = oi.UserId }).ToList();
            order.OrderItems = order.OrderItems.Where(oi => oi.Id == 0).ToList();
            _context.Orders.Add(order);
            _context.SaveChanges();
            // zabezpieczenie przed "tracking"
            orderItems.ForEach(oi =>
            {
                oi.OrderId = order.Id;
                var local = _context.Set<OrderItem>()
                    .Local
                    .FirstOrDefault(entry => entry.Id.Equals(oi.Id));

                if (local != null)
                {
                    _context.Entry(local).State = EntityState.Detached;
                }

                _context.Entry(oi).State = EntityState.Modified;
            });
            _context.OrderItem.UpdateRange(orderItems);
            _context.SaveChanges();
            DetachEntity(order);
            DetachEntity(order.OrderItems);
            return order.Id;
        }

        public Order GetOrderById(int id)
        {
            var order = _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .Include(inc => inc.Refund)
                .Include(inc => inc.Payment)
                .Include(inc => inc.Currency)
                .FirstOrDefault(o => o.Id == id);
            return order;
        }

        public IQueryable<Order> GetAllOrders()
        {
            return _context.Orders;
        }

        public IQueryable<OrderItem> GetAllOrderItems()
        {
            return _context.OrderItem
                .Include(inc => inc.Item).ThenInclude(inc => inc.Type)
                .Include(inc => inc.Item).ThenInclude(inc => inc.Brand);
        }

        public void UpdatedOrder(Order order)
        {
            var orderItems = order.OrderItems.ToList();
            // zabezpieczenie przed "tracking"
            orderItems.ForEach(oi =>
            {
                oi.OrderId = order.Id;
                var local = _context.Set<OrderItem>()
                    .Local
                    .FirstOrDefault(entry => entry.Id.Equals(oi.Id));

                if (local != null)
                {
                    _context.Entry(local).State = EntityState.Detached;
                }

                _context.Entry(oi).State = EntityState.Modified;
            });

            _context.Attach(order);
            _context.Entry(order).Property("Number").IsModified = true;
            _context.Entry(order).Property("Cost").IsModified = true;
            _context.Entry(order).Property("Ordered").IsModified = true;
            _context.Entry(order).Property("Delivered").IsModified = true;
            _context.Entry(order).Property("IsDelivered").IsModified = true;
            _context.Entry(order).Property("RefundId").IsModified = true;
            _context.Entry(order).Property("PaymentId").IsModified = true;
            _context.Entry(order).Property("IsPaid").IsModified = true;
            _context.Entry(order).Collection("OrderItems").IsModified = true;
            foreach (var orderItem in order.OrderItems)
            {
                if (orderItem.Id == 0)
                {
                    AddOrderItem(orderItem);
                }
                else
                {
                    _context.Attach(orderItem).Property("CouponUsedId").IsModified = true;
                    _context.Attach(orderItem).Property("RefundId").IsModified = true;
                    _context.Attach(orderItem).Property("ItemOrderQuantity").IsModified = true;
                }
            }

            _context.SaveChanges();
            _context.Entry(order).State = EntityState.Detached;
            DetachEntity(order.OrderItems);
        }

        private int AddOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Add(orderItem);
            _context.SaveChanges();
            return orderItem.Id;
        }
    }
}
