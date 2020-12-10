using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly Context _context;
        public OrderRepository(Context context)
        {
            _context = context;
        }

        public void DeleteOrder(int orderId)
        {
            var order = _context.Orders.Find(orderId);

            if (order != null)
            {
                _context.Orders.Remove(order);
                _context.SaveChanges();
            }
        }

        public int AddOrder(Order order)
        {
            _context.Orders.Add(order);
            _context.SaveChanges();
            return order.Id;
        }

        public Order GetOrderById(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
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
            return _context.OrderItem;
        }

        public IQueryable<Payment> GetAllPayments()
        {
            return _context.Payments;
        }

        public void DeletePayment(int paymentId)
        {
            var payment = _context.Payments.Find(paymentId);

            if (payment != null)
            {
                _context.Payments.Remove(payment);
                _context.SaveChanges();
            }
        }

        public int AddPayment(Payment payment)
        {
            _context.Payments.Add(payment);
            _context.SaveChanges();
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
            var refund = _context.Refunds.Find(refundId);

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
            _context.Attach(order);
            _context.Entry(order).Property("Number").IsModified = true;
            _context.Entry(order).Property("Cost").IsModified = true;
            _context.Entry(order).Property("Ordered").IsModified = true;
            _context.Entry(order).Property("Delivered").IsModified = true;
            _context.Entry(order).Property("IsDelivered").IsModified = true;
            _context.Entry(order).Property("RefundId").IsModified = true;
            _context.Entry(order).Property("PaymentId").IsModified = true;
            _context.Entry(order).Collection("OrderItems").IsModified = true;
            _context.SaveChanges();
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
            return _context.Coupons.FirstOrDefault(c => c.Id == id);
        }

        public IQueryable<OrderItem> GetAllOrderItemsByOrderId(int orderId)
        {
            return _context.OrderItem.Where(oi => oi.OrderId == orderId);
        }

        public Customer GetCustomerById(int id)
        {
            return _context.Customers.FirstOrDefault(c => c.Id == id);
        }

        public void AddOrderItems(List<OrderItem> orderItems)
        {
            orderItems.ForEach(oi =>
            {
                _context.OrderItem.Add(oi);
            });
            _context.SaveChanges();
        }
    }
}
