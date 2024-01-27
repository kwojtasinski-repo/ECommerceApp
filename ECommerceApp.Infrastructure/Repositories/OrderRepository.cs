using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly Context _context;

        public OrderRepository(Context context)
        {
            _context = context;
        }

        public bool DeleteOrder(int orderId)
        {
            var order = _context.Orders
                .Include(inc => inc.CouponUsed)
                .Include(inc => inc.Refund)
                .Include(p => p.OrderItems)
                .Select(o => new Order
                {
                        Id = o.Id,
                        CouponUsed = o.CouponUsed,
                        Refund = o.Refund,
                        OrderItems = o.OrderItems,
                        Payment = _context.Payments.FirstOrDefault(p => p.OrderId == orderId)
                })
                .FirstOrDefault(o => o.Id == orderId);
            var localOrder = _context.Orders.Local.FirstOrDefault(o => o.Id == orderId);
            if (localOrder is not null)
            {
                _context.Orders.Entry(localOrder).State = EntityState.Detached;
            }

            if (order == null)
            {
                return false;
            }

            foreach (var orderItem in order.OrderItems)
            {
                _context.OrderItem.Remove(orderItem);
            }

            if (order.Payment is not null)
            {
                _context.Payments.Remove(order.Payment);
            }

            if (order.CouponUsed is not null)
            {
                _context.CouponUsed.Remove(order.CouponUsed);
            }

            if (order.Refund is not null)
            {
                _context.Refunds.Remove(order.Refund);
            }

            _context.Orders.Remove(order);
            return _context.SaveChanges() > 0;
        }

        public int AddOrder(Order order)
        {
            _context.Orders.Add(order);
            _context.SaveChanges();
            return order.Id;
        }

        public Order GetOrderById(int id)
        {
            var order = _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .FirstOrDefault(o => o.Id == id);
            return order;
        }
        
        public Order GetOrderForRealizationById(int id)
        {
            var order = _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .Include(inc => inc.Currency)
                .Include(inc => inc.Customer)
                .FirstOrDefault(o => o.Id == id && !o.IsPaid);
            return order;
        }

        public List<Order> GetAllOrders()
        {
            return _context.Orders.ToList();
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
        }

        private int AddOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Add(orderItem);
            _context.SaveChanges();
            return orderItem.Id;
        }

        public Order GetOrderSummaryById(int orderId)
        {
            var order = _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .Include(inc => inc.Currency)
                .Include(inc => inc.Customer)
                .Include(inc => inc.CouponUsed).ThenInclude(inc => inc.Coupon)
                .FirstOrDefault(o => o.Id == orderId && !o.IsPaid);
            return order;
        }

        public Order GetOrderDetailsById(int id)
        {
            return _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .Include(inc => inc.Refund)
                .Include(inc => inc.Payment).ThenInclude(inc => inc.Currency)
                .Include(inc => inc.Currency)
                .Include(inc => inc.CouponUsed).ThenInclude(inc => inc.Coupon)
                .Include(inc => inc.Customer)
                .FirstOrDefault(o => o.Id == id);
        }

        public List<Order> GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            return _context.Orders
                           .Where(o => o.Number.StartsWith(searchString))
                           .Include(c => c.Currency)
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize).ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Orders
                           .Include(c => c.Currency)
                           .Where(o => o.Number.StartsWith(searchString))
                           .Count();
        }

        public Order GetOrderByRefundId(int refundId)
        {
            return _context.Orders
                           .Include(oi => oi.OrderItems)
                           .FirstOrDefault(r => r.RefundId == refundId);
        }

        public List<Order> GetAllOrders(int customerId, int pageSize, int pageNo)
        {
            return _context.Orders
                           .Where(o => o.CustomerId == customerId)
                           .Include(c => c.Currency)
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountByCustomerId(int customerId)
        {
            return _context.Orders
                           .Where(o => o.CustomerId == customerId)
                           .Include(c => c.Currency)
                           .Count();
        }

        public List<Order> GetAllOrders(int customerId)
        {
            return _context.Orders
                           .Where(o => o.CustomerId == customerId)
                           .ToList();
        }

        public List<Order> GetAllOrders(string userId, int pageSize, int pageNo)
        {
            return _context.Orders
                           .Where(o => o.UserId == userId)
                           .Include(c => c.Currency)
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountByUserId(string userId)
        {
            return _context.Orders
                           .Where(o => o.UserId == userId)
                           .Include(c => c.Currency)
                           .Count();
        }

        public List<Order> GetAllOrders(string userId)
        {
            return _context.Orders
                           .Where(o => o.UserId == userId)
                           .ToList();
        }

        public Order GetOrderPaidAndDeliveredById(int id)
        {
            return _context.Orders
                           .Include(oi => oi.OrderItems)
                           .FirstOrDefault(o => o.Id == id && o.IsPaid && o.IsDelivered);
        }

        public List<Order> GetAllPaidOrders(int pageSize, int pageNo, string searchString)
        {
            return _context.Orders
                           .Where(o => o.IsPaid == true && o.IsDelivered == false)
                           .Where(o => o.Number.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountPaidOrdersBySearchString(string searchString)
        {
            return _context.Orders
                           .Where(o => o.IsPaid == true && o.IsDelivered == false)
                           .Where(o => o.Number.StartsWith(searchString))
                           .Count();
        }
    }
}
