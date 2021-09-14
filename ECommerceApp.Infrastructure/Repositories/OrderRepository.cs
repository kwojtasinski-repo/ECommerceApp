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
        private readonly Context _context;

        public OrderRepository(Context context) : base(context)
        {
            _context = context;
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
            return order.Id;
        }

        public Order GetOrderById(int id)
        {
            var order = _context.Orders
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .Include(inc => inc.Refund)
                .Include(inc => inc.Payment)
                .FirstOrDefault(o => o.Id == id);
            return order;
        }

        public IQueryable<Order> GetAllOrders()
        {
            return _context.Orders;
        }

        public OrderItem GetOrderItemById(int id)
        {
            var orderItem = _context.OrderItem.FirstOrDefault(o => o.Id == id);
            return orderItem;
        }

        public IQueryable<OrderItem> GetAllOrderItems()
        {
            return _context.OrderItem
                .Include(inc => inc.Item).ThenInclude(inc => inc.Type)
                .Include(inc => inc.Item).ThenInclude(inc => inc.Brand);
        }

        public IQueryable<Payment> GetAllPayments()
        {
            return _context.Payments;
        }

        public void DeletePayment(int paymentId)
        {
            var payment = _context.Payments
                .Include(inc => inc.Order)
                .Include(inc => inc.Customer)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment != null)
            {
                var order = GetOrderById(payment.OrderId);
                order.IsPaid = false;
                UpdatedOrder(order);
                _context.Payments.Remove(payment);
                _context.SaveChanges();
            }
        }

        public int AddPayment(Payment payment)
        {
            _context.Payments.Add(payment);
            _context.SaveChanges();
            if(payment.Id > 0)
            {
                var order = GetOrderById(payment.OrderId);
                // should be only PaymentId and IsPaid 
                // the other properties should be added when customer got package
                order.PaymentId = payment.Id;
                order.IsPaid = true;
                order.Delivered = System.DateTime.Now;
                order.IsDelivered = true;
                UpdatedOrder(order);
            }
            return payment.Id;
        }

        public Payment GetPaymentById(int id)
        {
            var payment = _context.Payments.FirstOrDefault(p => p.Id == id);
            return payment;
        }

        public IQueryable<Refund> GetAllRefunds()
        {
            return _context.Refunds;
        }

        public void DeleteRefund(int refundId)
        {
            var refund = _context.Refunds
                .Include(inc => inc.Order)
                .Include(inc => inc.OrderItems)
                .FirstOrDefault(r => r.Id == refundId);

            if (refund != null)
            {
                _context.Refunds.Remove(refund);
                _context.SaveChanges();
            }
        }

        public int AddRefund(Refund refund)
        {
            _context.Refunds.Add(refund);
            _context.SaveChanges();
            return refund.Id;
        }

        public Refund GetRefundById(int id)
        {
            var refund = _context.Refunds.FirstOrDefault(r => r.Id == id);
            return refund;
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
    }

        public int AddOrderItem(OrderItem orderItem)
        {
            _context.OrderItem.Add(orderItem);
            _context.SaveChanges();
            return orderItem.Id;
        }

        public void UpdatePayment(Payment payment)
        {
            _context.Attach(payment);
            _context.Entry(payment).Property("Number").IsModified = true;
            _context.Entry(payment).Property("DateOfOrderPayment").IsModified = true;
            _context.SaveChanges();
        }

        public void UpdateRefund(Refund refund)
        {
            _context.Attach(refund);
            _context.Entry(refund).Property("Reason").IsModified = true;
            _context.Entry(refund).Property("Accepted").IsModified = true;
            _context.Entry(refund).Property("RefundDate").IsModified = true;
            _context.Entry(refund).Property("OnWarranty").IsModified = true;
            _context.Entry(refund).Property("CustomerId").IsModified = true;
            _context.Entry(refund).Property("OrderId").IsModified = true;
            _context.Entry(refund).Collection("OrderItems").IsModified = true;
            _context.SaveChanges();
        }

        public IQueryable<Item> GetAllItems()
        {
            return _context.Items;
        }

        public IQueryable<Customer> GetAllCustomers()
        {
            return _context.Customers;
        }

        public IQueryable<Coupon> GetAllCoupons()
        {
            return _context.Coupons;
        }

        public void UpdateCoupon(Coupon coupon, int couponUsedId)
        {
            _context.Attach(coupon);
            _context.Entry(coupon).Property("CouponUsedId").IsModified = true;
            _context.SaveChanges();
        }


        public int AddCouponUsed(CouponUsed couponUsed)
        {
            _context.CouponUsed.Add(couponUsed);
            _context.SaveChanges();
            return couponUsed.Id;
        }

        public Coupon GetCouponById(int id)
        {
            return _context.Coupons
                .Where(c => c.CouponUsedId == null)
                .FirstOrDefault(c => c.Id == id);
        }

        public IQueryable<OrderItem> GetAllOrderItemsByOrderId(int orderId)
        {
            return _context.OrderItem.Where(oi => oi.OrderId == orderId);
        }

        public Customer GetCustomerById(int id)
        {
            return _context.Customers.FirstOrDefault(c => c.Id == id);
        }

        public void AddOrderItems(ICollection<OrderItem> orderItems)
        {
            foreach(var orderItem in orderItems)
            {
                _context.OrderItem.Add(orderItem);
            }

            _context.SaveChanges();
        }

        public IQueryable<Customer> GetCustomersByUserId(string userId)
        {
            return _context.Customers.Where(c => c.UserId == userId);
        }

        public int AddCustomer(Customer customer)
        {
            _context.Customers.Add(customer);
            _context.SaveChanges();
            return customer.Id;
        }

        public void UpdateOrderItems(List<OrderItem> orderItems)
        {
            foreach(var orderItem in orderItems)
            {
                _context.Attach(orderItem);
                _context.Entry(orderItem).Property("ItemId").IsModified = true;
                _context.Entry(orderItem).Property("ItemOrderQuantity").IsModified = true;
                _context.Entry(orderItem).Property("UserId").IsModified = true;
                _context.Entry(orderItem).Property("OrderId").IsModified = true;
                _context.Entry(orderItem).Property("CouponUsedId").IsModified = true;
                _context.Entry(orderItem).Property("RefundId").IsModified = true;
            }
            _context.SaveChanges();
        }

        public void UpdateOrderItem(OrderItem orderItem)
        {
            _context.Attach(orderItem);
            _context.Entry(orderItem).Property("ItemId").IsModified = true;
            _context.Entry(orderItem).Property("ItemOrderQuantity").IsModified = true;
            _context.Entry(orderItem).Property("UserId").IsModified = true;
            _context.Entry(orderItem).Property("OrderId").IsModified = true;
            _context.Entry(orderItem).Property("CouponUsedId").IsModified = true;
            _context.Entry(orderItem).Property("RefundId").IsModified = true;
            _context.SaveChanges();
        }

        public OrderItem GetOrderItemNotOrdered(OrderItem orderItem)
        {
            var item = _context.OrderItem.FirstOrDefault(oi => oi.ItemId == orderItem.ItemId && oi.OrderId == null);
            return item;
        }

        public OrderItem GetOrderItemNotOrderedByItemId(int itemId, string userId)
        {
            var orderItem = _context.OrderItem.FirstOrDefault(oi => oi.ItemId == itemId && oi.UserId == userId && oi.OrderId == null);
            return orderItem;
        }

        public void RemoveOrderedItems(int orderId)
        {
            var orderItems = _context.OrderItem.Where(oi => oi.OrderId == orderId).ToList();
            orderItems.ForEach(oi =>
            {
                var item = _context.Items.Find(oi.ItemId);
                item.Quantity -= oi.ItemOrderQuantity;
                _context.SaveChanges();
            });
        }

        public void DeleteOrderItem(int id)
        {
            var orderItem = _context.OrderItem.Find(id);
            if(orderItem!=null)
            {
                _context.OrderItem.Remove(orderItem);
                _context.SaveChanges();
            }
        }
    }
}
