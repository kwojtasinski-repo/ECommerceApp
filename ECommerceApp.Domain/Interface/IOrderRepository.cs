using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        void DeleteOrder(int orderId);
        int AddOrder(Order order);
        Order GetOrderById(int id);
        IQueryable<Order> GetAllOrders();
        OrderItem GetOrderItemById(int id);
        IQueryable<OrderItem> GetAllOrderItems();
        IQueryable<Refund> GetAllRefunds();
        void DeleteRefund(int refundId);
        int AddRefund(Refund refund);
        Refund GetRefundById(int id);
        void UpdatedOrder(Order order);
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
        int AddOrderItem(OrderItem orderItem);
        int AddCustomer(Customer customer);
        void UpdateOrderItems(List<OrderItem> orderItems);
        void UpdateOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemNotOrdered(OrderItem orderItem);
        OrderItem GetOrderItemNotOrderedByItemId(int itemId, string userId);
        void RemoveOrderedItems(int orderId);
        void DeleteOrderItem(int id);
    }
}
