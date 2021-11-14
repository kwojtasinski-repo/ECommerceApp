using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
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
        int AddOrder(NewOrderVm order);
        void UpdateOrder(NewOrderVm order);
        void DeleteOrder(int id);
        ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        NewOrderVm GetOrderForEdit(int id);
        OrderDetailsVm GetOrderDetail(int id);
        List<OrderForListVm> GetAllOrders();
        void AddCouponToOrder(int orderId, int couponUsedId);
        void DeleteRefund(int id);
        ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString);
        ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        IQueryable<NewCustomerForOrdersVm> GetAllCustomers();
        IQueryable<ECommerceApp.Domain.Model.Item> GetAllItemsToOrder();
        void DeleteCouponUsed(int orderId, int couponUsedId);
        IQueryable<CouponVm> GetAllCoupons();
        int CheckPromoCode(string code);
        int UpdateCoupon(int couponId, NewOrderVm order);
        List<OrderItemForListVm> GetAllItemsOrdered();
        IQueryable<NewCustomerForOrdersVm> GetCustomersByUserId(string userId);
        NewOrderVm GetOrderById(int orderId);
        void CalculateCost(NewOrderVm order, NewOrderItemVm model);
        NewCustomerForOrdersVm GetCustomerById(int id);
        void AddOrderItems(List<NewOrderItemVm> orderItems);
        OrderItemDetailsVm GetOrderItemDetail(int id);
        bool CheckEnteredRefund(string reasonRefund);
        ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        int AddOrderItem(NewOrderItemVm model);
        List<ItemsAddToCartVm> GetItemsAddToCart();
        ListForItemOrderVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        List<NewOrderItemVm> GetOrderItemsNotOrderedByUserId(string userId);
        List<OrderItemForListVm> GetOrderItemsNotOrderedByUser(string userId);
        int AddCustomer(NewCustomerVm newCustomer);
        void UpdateOrderItems(List<NewOrderItemVm> orderItems);
        int OrderItemCount(string userId);
        void UpdateOrderItem(OrderItemForListVm model);
        int AddItemToOrderItem(int itemId, string userId);
        void DeleteOrderItem(int id);
        List<OrderItemForListVm> GetAllItemsOrderedByItemId(int id);
        bool CheckIfOrderExists(int id);
        bool CheckIfRefundExists(int id);
        bool CheckIfOrderItemExists(int id);
        List<OrderForListVm> GetAllOrdersByUserId(string userId);
        List<OrderForListVm> GetAllOrders(Expression<Func<Order,bool>> expression);
        void AddRefund(int orderId, int refundId);
    }
}
