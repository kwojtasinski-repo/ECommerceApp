using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IOrderService
    {
        int AddOrder(NewOrderVm order);
        void UpdateOrder(NewOrderVm order);
        void DeleteOrder(int id);
        ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        NewOrderVm GetOrderForEdit(int id);
        OrderDetailsVm GetOrderDetail(int id);
        int AddPayment(NewPaymentVm payment);
        void UpdatePayment(NewPaymentVm refund);
        void DeletePayment(int id);
        ListForPaymentVm GetAllPayments(int pageSize, int pageNo, string searchString);
        NewPaymentVm GetPaymentForEdit(int id);
        PaymentDetailsVm GetPaymentDetail(int id);
        int AddRefund(NewRefundVm refundVm);
        void UpdateRefund(NewRefundVm refundVm);
        void DeleteRefund(int id);
        ListForRefundVm GetAllRefunds(int pageSize, int pageNo, string searchString);
        NewRefundVm GetRefundForEdit(int id);
        RefundDetailsVm GetRefundDetail(int id);
        ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString);
        ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        IQueryable<NewCustomerForOrdersVm> GetAllCustomers();
        IQueryable<ECommerceApp.Domain.Model.Item> GetAllItemsToOrder();
        IQueryable<NewCouponVm> GetAllCoupons();
        int CheckPromoCode(string code);
        void UpdateCoupon(int id, int orderId);
        NewOrderVm GetOrderById(int orderId);
        void CalculateCost(NewOrderVm order, NewOrderItemVm model);
        NewCustomerForOrdersVm GetCustomerById(int id);
        void AddOrderItems(List<NewOrderItemVm> orderItems);
    }
}
