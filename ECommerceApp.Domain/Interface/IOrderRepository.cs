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
        IQueryable<OrderItem> GetAllOrderItems();
        IQueryable<Refund> GetAllRefunds();
        void UpdatedOrder(Order order);
        IQueryable<Item> GetAllItems();
        IQueryable<Coupon> GetAllCoupons();
        void UpdateCoupon(Coupon coupon, int couponUsedId);
        Coupon GetCouponById(int id);
        int AddCouponUsed(CouponUsed couponUsed);
        Customer GetCustomerById(int id);
        IQueryable<Customer> GetCustomersByUserId(string userId);
        int AddOrderItem(OrderItem orderItem);
        int AddCustomer(Customer customer);
        void UpdateOrderItem(OrderItem orderItem);
        OrderItem GetOrderItemNotOrdered(OrderItem orderItem);
    }
}
