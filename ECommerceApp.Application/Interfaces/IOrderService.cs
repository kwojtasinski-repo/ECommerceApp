using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IOrderService : IAbstractService<OrderVm, IOrderRepository, Order>
    {
        int AddOrder(OrderVm order);
        void UpdateOrder(OrderVm order);
        void DeleteOrder(int id);
        ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        OrderDetailsVm GetOrderDetail(int id);
        List<OrderForListVm> GetAllOrders();
        void AddCouponToOrder(int orderId, int couponUsedId);
        void DeleteRefundFromOrder(int id);
        void DeleteCouponUsedFromOrder(int orderId, int couponUsedId);
        int AddCouponToOrder(int couponId, NewOrderVm order);
        OrderVm GetOrderById(int orderId);
        ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        List<OrderForListVm> GetAllOrdersByUserId(string userId);
        List<OrderForListVm> GetAllOrders(Expression<Func<Order,bool>> expression);
        void AddRefundToOrder(int orderId, int refundId);
        NewOrderVm GetOrderForRealization(int orderId);
        ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString);
        void DispatchOrder(int orderId);
    }
}
