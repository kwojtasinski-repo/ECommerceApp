﻿using AutoMapper;
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
    public class OrderService : OrderServiceAbstract
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IMapper _mapper;

        public OrderService(IOrderRepository orderRepo, IMapper mapper) : base(orderRepo, mapper)
        {
            _orderRepo = orderRepo;
            _mapper = mapper;
        }

        public override int AddOrder(NewOrderVm model)
        {
            return Add(model);
        }

        public override int AddPayment(NewPaymentVm paymentVm)
        {
            var payment = _mapper.Map<Payment>(paymentVm);
            var id = _orderRepo.AddPayment(payment);
            _orderRepo.RemoveOrderedItems(payment.OrderId);
            return id;
        }

        public override int AddRefund(NewRefundVm refundVm)
        {
            var refund = _mapper.Map<Refund>(refundVm);
            var id = _orderRepo.AddRefund(refund);
            return id;
        }

        public override void DeleteOrder(int id)
        {
            Delete(id);
        }

        public override void DeletePayment(int id)
        {
            _orderRepo.DeletePayment(id);
        }

        public override void DeleteRefund(int id)
        {
            _orderRepo.DeleteRefund(id);
        }

        public override IQueryable<Item> GetAllItemsToOrder()
        {
            var items = _orderRepo.GetAllItems();
            return items;
        }

        public override IQueryable<NewOrderItemVm> GetAllItemsOrderedForAdd()
        {
            var itemOrders = _orderRepo.GetAllOrderItems()
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider);

            return itemOrders;
        }

        public override ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString)
        {
            var itemOrder = _orderRepo.GetAllOrderItems().Where(oi => oi.Item.Name.StartsWith(searchString) ||
                            oi.Item.Brand.Name.StartsWith(searchString) || oi.Item.Type.Name.StartsWith(searchString))
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForItemOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public override List<OrderItemForListVm> GetAllItemsOrdered()
        {
            var itemOrder = _orderRepo.GetAllOrderItems()
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            
            return itemOrder;
        }

        public override ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo)
        {
            var itemOrder = _orderRepo.GetAllOrderItems().Where(oi => oi.ItemId == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForItemOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
        }

        public override List<OrderItemForListVm> GetAllItemsOrderedByItemId(int id)
        {
            var itemOrder = _orderRepo.GetAllOrderItems().Where(oi => oi.ItemId == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            
            return itemOrder;
        }

        public override ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.Number.ToString().StartsWith(searchString))
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var ordersToShow = orders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Orders = ordersToShow,
                Count = orders.Count
            };

            return ordersList;
        }

        public override List<OrderForListVm> GetAllOrders()
        {
            return _orderRepo.GetAllOrders()
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public override ListForPaymentVm GetAllPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _orderRepo.GetAllPayments().Where(p => p.Number.ToString().StartsWith(searchString))
                            .ProjectTo<PaymentForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var paymentsToShow = payments.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var paymentsList = new ListForPaymentVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Payments = paymentsToShow,
                Count = payments.Count
            };

            return paymentsList;
        }

        public override List<PaymentForListVm> GetAllPayments()
        {
            var payments = _orderRepo.GetAllPayments()
                            .ProjectTo<PaymentForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return payments;
        }

        public override ListForRefundVm GetAllRefunds(int pageSize, int pageNo, string searchString)
        {
            var refunds = _orderRepo.GetAllRefunds().Where(r => r.Reason.StartsWith(searchString) 
                            || r.RefundDate.ToString().StartsWith(searchString))
                            .ProjectTo<RefundForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var refundsToShow = refunds.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var refundsList = new ListForRefundVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Refunds = refundsToShow,
                Count = refunds.Count
            };

            return refundsList;
        }

        public override List<RefundForListVm> GetAllRefunds()
        {
            var refunds = _orderRepo.GetAllRefunds()
                            .ProjectTo<RefundForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return refunds;
        }

        public override OrderDetailsVm GetOrderDetail(int id)
        {
            var orderDetails = _orderRepo.GetOrderById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        public override NewOrderVm GetOrderForEdit(int id)
        {
            return Get(id);
        }

        public override PaymentDetailsVm GetPaymentDetail(int id)
        {
            var paymentDetails = _orderRepo.GetPaymentById(id);
            var paymentDetailsVm = _mapper.Map<PaymentDetailsVm>(paymentDetails);
            return paymentDetailsVm;
        }

        public override NewPaymentVm GetPaymentForEdit(int id)
        {
            var payment = _orderRepo.GetPaymentById(id);
            var paymentVm = _mapper.Map<NewPaymentVm>(payment);
            return paymentVm;
        }

        public override RefundDetailsVm GetRefundDetail(int id)
        {
            var refundDetails = _orderRepo.GetRefundById(id);
            var refundDetailsVm = _mapper.Map<RefundDetailsVm>(refundDetails);
            return refundDetailsVm;
        }

        public override NewRefundVm GetRefundForEdit(int id)
        {
            var refund = _orderRepo.GetRefundById(id);
            var refundVm = _mapper.Map<NewRefundVm>(refund);
            return refundVm;
        }

        public override void UpdateOrder(NewOrderVm orderVm)
        {
            Update(orderVm);
        }

        public override void UpdatePayment(NewPaymentVm paymentVm)
        {
            var payment = _mapper.Map<Payment>(paymentVm);
            _orderRepo.UpdatePayment(payment);
        }

        public override void UpdateRefund(NewRefundVm refundVm)
        {
            var refund = _mapper.Map<Refund>(refundVm);
            _orderRepo.UpdateRefund(refund);
        }

        public override IQueryable<NewCustomerForOrdersVm> GetAllCustomers()
        {
            var customers = _orderRepo.GetAllCustomers();
            var customersVm = customers.ProjectTo<NewCustomerForOrdersVm>(_mapper.ConfigurationProvider);
            return customersVm;
        }

        public override IQueryable<NewCouponVm> GetAllCoupons()
        {
            var coupons = _orderRepo.GetAllCoupons();
            var couponsVm = coupons.ProjectTo<NewCouponVm>(_mapper.ConfigurationProvider);
            return couponsVm;
        }

        public override int CheckPromoCode(string code)
        {
            var coupons = GetAllCoupons().ToList();
         //   var coupon = coupons.FirstOrDefault(c => c.Code. == code && c.CouponUsedId == null);
            var coupon = coupons.FirstOrDefault(c => String.Equals(c.Code, code,
                   StringComparison.Ordinal) && c.CouponUsedId == null);
            var id = 0;
            if (coupon != null)
            {
                id = coupon.Id;
            }
            return id;
        }

        public override int UpdateCoupon(int couponId, NewOrderVm order)
        {
            var couponUsed = new NewCouponUsedVm()
            {
                Id = 0,
                CouponId = couponId,
                OrderId = order.Id
            };
            var couponUsedId = AddCouponUsed(couponUsed);
            var coupon = _orderRepo.GetCouponById(couponId);
            _orderRepo.UpdateCoupon(coupon, couponUsedId);
            order.Cost = (1 - (decimal)coupon.Discount/100) * order.Cost;
            return couponUsedId;
        }

        public override int AddCouponUsed(NewCouponUsedVm couponUsedVm)
        {
            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            var id = _orderRepo.AddCouponUsed(couponUsed);
            return id;
        }

        public override NewOrderVm GetOrderById(int orderId)
        {
            var order = _orderRepo.GetOrderById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public override void CalculateCost(NewOrderVm order, NewOrderItemVm model)
        {
            throw new NotImplementedException();
        }

        public override NewCustomerForOrdersVm GetCustomerById(int id)
        {
            var customer = _orderRepo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerForOrdersVm>(customer);
            return customerVm;
        }

        public override void AddOrderItems(List<NewOrderItemVm> orderItemsVm)
        {
            var orderItems = _mapper.Map<List<NewOrderItemVm>, List<OrderItem>>(orderItemsVm);
            _orderRepo.AddOrderItems(orderItems);
        }

        public override NewPaymentVm GetPaymentById(int id)
        {
            var payment = _orderRepo.GetPaymentById(id);
            var paymentVm = _mapper.Map<NewPaymentVm>(payment);
            return paymentVm;
        }

        public override OrderItemDetailsVm GetOrderItemDetail(int id)
        {
            var orderItem = _orderRepo.GetOrderItemById(id);
            var orderItemVm = _mapper.Map<OrderItemDetailsVm>(orderItem);
            return orderItemVm;
        }

        public override bool CheckEnteredRefund(string reasonRefund)
        {
            var refunds = _orderRepo.GetAllRefunds().ToList();
            var refund = refunds.FirstOrDefault(r => String.Equals(r.Reason, reasonRefund,
                   StringComparison.OrdinalIgnoreCase));
            if (refund != null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public override ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.CustomerId == customerId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var ordersToShow = orders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                Orders = ordersToShow,
                Count = orders.Count
            };

            return ordersList;
        }

        public override List<OrderForListVm> GetAllOrdersByCustomerId(int customerId)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.CustomerId == customerId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return orders;
        }

        public override IQueryable<NewCustomerForOrdersVm> GetCustomersByUserId(string userId)
        {
            var customers = _orderRepo.GetCustomersByUserId(userId);
            var customersVm = customers.ProjectTo<NewCustomerForOrdersVm>(_mapper.ConfigurationProvider);
            return customersVm;
        }

        public override ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.UserId == userId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            var ordersToShow = orders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                Orders = ordersToShow,
                Count = orders.Count
            };

            return ordersList;
        }

        public override List<OrderForListVm> GetAllOrdersByUserId(string userId)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.UserId == userId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return orders;
        }

        public override int AddOrderItem(NewOrderItemVm model)
        {
            var orderItem = _mapper.Map<OrderItem>(model);
            var orderItemExist = GetOrderItem(orderItem);
            int id;
            if(orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _orderRepo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                id = _orderRepo.AddOrderItem(orderItem);
            }
            return id;
        }

        public override List<ItemsAddToCartVm> GetItemsAddToCart()
        {
            var items = GetAllItemsToOrder();
            var itemsVm = items.ProjectTo<ItemsAddToCartVm>(_mapper.ConfigurationProvider).ToList();
            return itemsVm;
        }

        public override ListForItemOrderVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo)
        {
            var itemOrders = _orderRepo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            var itemOrdersToShow = itemOrders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForItemOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrdersToShow,
                Count = itemOrders.Count
            };

            return ordersList;
        }

        public override List<NewOrderItemVm> GetOrderItemsNotOrderedByUserId(string userId)
        {
            var itemOrders = _orderRepo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return itemOrders;
        }

        public override int AddCustomer(NewCustomerVm newCustomer)
        {
            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _orderRepo.AddCustomer(customer);
            return id;
        }

        public override void UpdateOrderItems(List<NewOrderItemVm> orderItemsVm)
        {
            var orderItems = _mapper.Map<List<NewOrderItemVm>, List<OrderItem>>(orderItemsVm);
            _orderRepo.UpdateOrderItems(orderItems);
        }

        public override int OrderItemCount(string userId)
        {
            var itemOrders = _orderRepo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return itemOrders.Count;
        }

        public override void UpdateOrderItem(OrderItemForListVm orderItemVm)
        {
            var orderItem = _mapper.Map<OrderItemForListVm, OrderItem>(orderItemVm);
            _orderRepo.UpdateOrderItem(orderItem);
        }

        public override int AddItemToOrderItem(int itemId, string userId)
        {
            var orderItemExist = GetOrderItemByItemId(itemId, userId);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _orderRepo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                var orderItem = CreateOrderItem(itemId, userId);
                id = _orderRepo.AddOrderItem(orderItem);
            }
            return id;
        }

        private OrderItem CreateOrderItem(int itemId, string userId)
        {
            var orderItem = new OrderItem()
            {
                Id = 0,
                ItemId = itemId,
                ItemOrderQuantity = 1,
                UserId = userId,
            };
            return orderItem;
        }

        private OrderItem GetOrderItem(OrderItem orderItem)
        {
            var item = _orderRepo.GetOrderItemNotOrdered(orderItem);
            return item;
        }

        private OrderItem GetOrderItemByItemId(int itemId, string userId)
        {
            var orderItem = _orderRepo.GetOrderItemNotOrderedByItemId(itemId, userId);
            return orderItem;
        }

        public override void DeleteOrderItem(int id)
        {
            _orderRepo.DeleteOrderItem(id);
        }

        public override bool CheckIfOrderExists(int id)
        {
            var order = _orderRepo.GetOrderById(id);
            if (order == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfPaymentExists(int id)
        {
            var payment = _orderRepo.GetPaymentById(id);
            if (payment == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfRefundExists(int id)
        {
            var refund = _orderRepo.GetRefundById(id);
            if (refund == null)
            {
                return false;
            }
            return true;
        }

        public override bool CheckIfOrderItemExists(int id)
        {
            var orderItem = _orderRepo.GetOrderItemById(id);
            if (orderItem == null)
            {
                return false;
            }
            return true;
        }
    }
}
