using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IMapper _mapper;

        public OrderService(IOrderRepository orderRepo, IMapper mapper)
        {
            _orderRepo = orderRepo;
            _mapper = mapper;
        }

        public int AddOrder(NewOrderVm orderVm)
        {
            var order = _mapper.Map<Order>(orderVm);
            var id = _orderRepo.AddOrder(order);
            return id;
        }

        public int AddPayment(NewPaymentVm paymentVm)
        {
            var payment = _mapper.Map<Payment>(paymentVm);
            var id = _orderRepo.AddPayment(payment);
            return id;
        }

        public int AddRefund(NewRefundVm refundVm)
        {
            var refund = _mapper.Map<Refund>(refundVm);
            var id = _orderRepo.AddRefund(refund);
            return id;
        }

        public void DeleteOrder(int id)
        {
            _orderRepo.DeleteOrder(id);
        }

        public void DeletePayment(int id)
        {
            _orderRepo.DeletePayment(id);
        }

        public void DeleteRefund(int id)
        {
            _orderRepo.DeleteRefund(id);
        }

        public IQueryable<Item> GetAllItemsToOrder()
        {
            var items = _orderRepo.GetAllItems();
            return items;
        }

        public IQueryable<NewOrderItemVm> GetAllItemsOrderedForAdd()
        {
            var itemOrders = _orderRepo.GetAllOrderItems()
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider);

            return itemOrders;
        }

        public ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString)
        {
            var itemOrder = _orderRepo.GetAllOrderItems().Where(oi => oi.Item.Name.StartsWith(searchString))
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForItemOrderVm()
            {
                PageSize = pageNo,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo)
        {
            var itemOrder = _orderRepo.GetAllOrderItems().Where(oi => oi.Id == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForItemOrderVm()
            {
                PageSize = pageNo,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.Number.ToString().StartsWith(searchString))
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var ordersToShow = orders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageNo,
                CurrentPage = pageNo,
                SearchString = searchString,
                Orders = ordersToShow,
                Count = orders.Count
            };

            return ordersList;
        }

        public ListForPaymentVm GetAllPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _orderRepo.GetAllPayments().Where(p => p.Number.ToString().StartsWith(searchString))
                            .ProjectTo<PaymentForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var paymentsToShow = payments.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var paymentsList = new ListForPaymentVm()
            {
                PageSize = pageNo,
                CurrentPage = pageNo,
                SearchString = searchString,
                Payments = paymentsToShow,
                Count = payments.Count
            };

            return paymentsList;
        }

        public ListForRefundVm GetAllRefunds(int pageSize, int pageNo, string searchString)
        {
            var refunds = _orderRepo.GetAllRefunds().Where(r => r.Reason.StartsWith(searchString) 
                            || r.RefundDate.ToString().StartsWith(searchString))
                            .ProjectTo<RefundForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var refundsToShow = refunds.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var refundsList = new ListForRefundVm()
            {
                PageSize = pageNo,
                CurrentPage = pageNo,
                SearchString = searchString,
                Refunds = refundsToShow,
                Count = refunds.Count
            };

            return refundsList;
        }
      
        public OrderDetailsVm GetOrderDetail(int id)
        {
            var orderDetails = _orderRepo.GetOrderById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        public NewOrderVm GetOrderForEdit(int id)
        {
            var order = _orderRepo.GetOrderById(id);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public PaymentDetailsVm GetPaymentDetail(int id)
        {
            var paymentDetails = _orderRepo.GetPaymentById(id);
            var paymentDetailsVm = _mapper.Map<PaymentDetailsVm>(paymentDetails);
            return paymentDetailsVm;
        }

        public NewPaymentVm GetPaymentForEdit(int id)
        {
            var payment = _orderRepo.GetPaymentById(id);
            var paymentVm = _mapper.Map<NewPaymentVm>(payment);
            return paymentVm;
        }

        public RefundDetailsVm GetRefundDetail(int id)
        {
            var refundDetails = _orderRepo.GetRefundById(id);
            var refundDetailsVm = _mapper.Map<RefundDetailsVm>(refundDetails);
            return refundDetailsVm;
        }

        public NewRefundVm GetRefundForEdit(int id)
        {
            var refund = _orderRepo.GetRefundById(id);
            var refundVm = _mapper.Map<NewRefundVm>(refund);
            return refundVm;
        }

        public void UpdateOrder(NewOrderVm orderVm)
        {
            var order = _mapper.Map<Order>(orderVm);
            _orderRepo.UpdatedOrder(order);
        }

        public void UpdatePayment(NewPaymentVm paymentVm)
        {
            var payment = _mapper.Map<Payment>(paymentVm);
            _orderRepo.UpdatePayment(payment);
        }

        public void UpdateRefund(NewRefundVm refundVm)
        {
            var refund = _mapper.Map<Refund>(refundVm);
            _orderRepo.UpdateRefund(refund);
        }

        public IQueryable<NewCustomerForOrdersVm> GetAllCustomers()
        {
            var customers = _orderRepo.GetAllCustomers();
            var customersVm = customers.ProjectTo<NewCustomerForOrdersVm>(_mapper.ConfigurationProvider);
            return customersVm;
        }

        public IQueryable<NewCouponVm> GetAllCoupons()
        {
            var coupons = _orderRepo.GetAllCoupons();
            var couponsVm = coupons.ProjectTo<NewCouponVm>(_mapper.ConfigurationProvider);
            return couponsVm;
        }

        public int CheckPromoCode(string code)
        {
            var coupons = GetAllCoupons().ToList();
            var coupon = coupons.FirstOrDefault(c => c.Code == code);
            var id = 0;
            if (coupon != null)
            {
                id = coupon.Id;
            }
            return id;
        }

        public int UpdateCoupon(int couponId, int orderId)
        {
            var couponUsed = new NewCouponUsedVm()
            {
                Id = 0,
                CouponId = couponId,
                OrderId = orderId
            };
            var couponUsedId = AddCouponUsed(couponUsed);
            var coupon = _orderRepo.GetCouponById(couponId);
            _orderRepo.UpdateCoupon(coupon, couponUsedId);
            return couponUsedId;
        }

        public int AddCouponUsed(NewCouponUsedVm couponUsedVm)
        {
            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            var id = _orderRepo.AddCouponUsed(couponUsed);
            return id;
        }

        public NewOrderVm GetOrderById(int orderId)
        {
            var order = _orderRepo.GetOrderById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public void CalculateCost(NewOrderVm order, NewOrderItemVm model)
        {
            throw new NotImplementedException();
        }

        public NewCustomerForOrdersVm GetCustomerById(int id)
        {
            var customer = _orderRepo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerForOrdersVm>(customer);
            return customerVm;
        }

        public void AddOrderItems(List<NewOrderItemVm> orderItemsVm)
        {
            var orderItems = _mapper.Map<List<NewOrderItemVm>, List<OrderItem>>(orderItemsVm);
            _orderRepo.AddOrderItems(orderItems);
        }
    }
}
