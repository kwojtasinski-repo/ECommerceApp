﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Orders
{
    public interface IOrderService
    {
        OrderDto Get(int id);
        int AddOrder(AddOrderDto order);
        bool DeleteOrder(int id);
        ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        OrderDetailsVm GetOrderDetail(int id);
        List<OrderForListVm> GetAllOrders();
        void AddCouponUsedToOrder(int orderId, int couponUsedId);
        void DeleteRefundFromOrder(int id);
        void DeleteCouponUsedFromOrder(int orderId, int couponUsedId);
        int AddCouponToOrder(int couponId, NewOrderVm order);
        OrderDto GetOrderById(int orderId);
        ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        List<OrderForListVm> GetAllOrdersByUserId(string userId);
        List<OrderForListVm> GetAllOrders(Expression<Func<Order, bool>> expression);
        void AddRefundToOrder(int orderId, int refundId);
        NewOrderVm GetOrderForRealization(int orderId);
        ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString);
        void DispatchOrder(int orderId);
        OrderDto GetOrderByIdReadOnly(int id);
        int AddOrderFromCart(AddOrderFromCartDto model);
        OrderVm InitOrder();
        int FulfillOrder(OrderVm model);
        NewOrderVm GetOrderSummaryById(int id);
        NewOrderVm BuildVmForEdit(int orderId);
        OrderDetailsVm UpdateOrder(UpdateOrderDto dto);
    }
}
