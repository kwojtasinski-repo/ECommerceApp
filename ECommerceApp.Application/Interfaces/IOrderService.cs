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
        void DeleteRefund(int id);
        ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        IQueryable<ECommerceApp.Domain.Model.Item> GetAllItemsToOrder();
        void DeleteCouponUsed(int orderId, int couponUsedId);
        IQueryable<CouponVm> GetAllCoupons();
        int CheckPromoCode(string code);
        int UpdateCoupon(int couponId, NewOrderVm order);
        IQueryable<NewCustomerForOrdersVm> GetCustomersByUserId(string userId);
        OrderVm GetOrderById(int orderId);
        NewCustomerForOrdersVm GetCustomerById(int id);
        bool CheckEnteredRefund(string reasonRefund);
        ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        int AddOrderItem(NewOrderItemVm model);
        List<ItemsAddToCartVm> GetItemsAddToCart();
        ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        int AddCustomer(NewCustomerVm newCustomer);
        List<OrderForListVm> GetAllOrdersByUserId(string userId);
        List<OrderForListVm> GetAllOrders(Expression<Func<Order,bool>> expression);
        void AddRefund(int orderId, int refundId);
        NewOrderVm GetOrderForRealization(int orderId);
    }
}
