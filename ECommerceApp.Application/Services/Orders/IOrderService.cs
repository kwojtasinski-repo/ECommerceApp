using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Orders
{
    public interface IOrderService : IAbstractService<OrderVm, IOrderRepository, Order>
    {
        int AddOrder(OrderVm order);
        void UpdateOrder(OrderVm order);
        void DeleteOrder(int id);
        ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        OrderDetailsVm GetOrderDetail(int id);
        List<OrderForListVm> GetAllOrders();
        void AddCouponUsedToOrder(int orderId, int couponUsedId);
        void DeleteRefundFromOrder(int id);
        void DeleteCouponUsedFromOrder(int orderId, int couponUsedId);
        int AddCouponToOrder(int couponId, NewOrderVm order);
        OrderVm GetOrderById(int orderId);
        ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        List<OrderForListVm> GetAllOrdersByUserId(string userId);
        List<OrderForListVm> GetAllOrders(Expression<Func<Order, bool>> expression);
        void AddRefundToOrder(int orderId, int refundId);
        NewOrderVm GetOrderForRealization(int orderId);
        ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString);
        void DispatchOrder(int orderId);
        void UpdateOrderWithExistedOrderItemsIds(OrderVm vm);
        OrderVm GetOrderByIdReadOnly(int id);
        int GetOrderNumber(int orderId);
    }
}
