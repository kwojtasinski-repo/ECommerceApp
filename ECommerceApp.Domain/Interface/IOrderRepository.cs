using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository
    {
        void DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        IQueryable<Order> GetAllOrders();
        OrderItem GetOrderItemById(int id);
        IQueryable<OrderItem> GetAllOrderItems();
        IQueryable<Payment> GetAllPayments();
        void DeletePayment(int paymentId);
        int AddPayment(Payment payment);
        Payment GetPaymentById(int id);
        IQueryable<Refund> GetAllRefunds();
        void DeleteRefund(int refundId);
        int AddRefund(Refund refund);
        Refund GetRefundById(int id);
        void UpdatedOrder(Order order);
        void UpdatePayment(Payment payment);
        void UpdateRefund(Refund refund);
        IQueryable<Item> GetAllItems();
        IQueryable<Customer> GetAllCustomers();
        IQueryable<Coupon> GetAllCoupons();
        void UpdateCoupon(Coupon coupon, int couponUsedId);
        Coupon GetCouponById(int id);
        int AddCouponUsed(CouponUsed couponUsed);
        IQueryable<OrderItem> GetAllOrderItemsByOrderId(int orderId);
        Customer GetCustomerById(int id);
        void AddOrderItems(ICollection<OrderItem> orderItems);
        IQueryable<Customer> GetCustomersByUserId(string userId);
    }
}
