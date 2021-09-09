using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public abstract class OrderServiceAbstract : IBaseService<NewOrderVm>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IMapper _mapper;

        public OrderServiceAbstract(IOrderRepository orderRepo, IMapper mapper)
        {
            _orderRepo = orderRepo;
            _mapper = mapper;
        }

        public int Add(NewOrderVm objectVm)
        {
            if (objectVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var orderVm = new NewOrderVm()
            {
                Id = objectVm.Id,
                Number = objectVm.Number,
                Cost = objectVm.Cost,
                Ordered = objectVm.Ordered,
                Delivered = objectVm.Delivered,
                IsDelivered = objectVm.IsDelivered,
                CouponUsedId = objectVm.CouponUsedId,
                CustomerId = objectVm.CustomerId,
                UserId = objectVm.UserId,
                PaymentId = objectVm.PaymentId,
                IsPaid = objectVm.IsPaid,
                RefundId = objectVm.RefundId,
                OrderItems = objectVm.OrderItems.Select(oi => new NewOrderItemVm { Id = oi.Id, CouponUsedId = oi.CouponUsedId, ItemCost = oi.ItemCost, ItemId = oi.ItemId, ItemName = oi.ItemName, ItemOrderQuantity = oi.ItemOrderQuantity, OrderId = oi.OrderId, RefundId = oi.RefundId, UserId = oi.UserId }).ToList()
            };
            //orderVm.OrderItems = orderVm.OrderItems.Where(oi => oi.Id == 0).ToList();
            var order = _mapper.Map<Order>(orderVm);
            var id = _orderRepo.AddOrder(order);

            /*foreach(var orderItem in objectVm.OrderItems)
            {
                if(orderItem.Id != 0)
                {
                    var oi = new OrderItem()
                    {
                        Id = orderItem.Id,
                        CouponUsedId = orderItem.CouponUsedId,
                        ItemId = orderItem.ItemId,
                        ItemOrderQuantity = orderItem.ItemOrderQuantity,
                        OrderId = id,
                        RefundId = orderItem.RefundId,
                        UserId = orderItem.UserId
                    };

                    _orderRepo.UpdateOrderItem(oi);
                }
            }*/

            return id;
        }

        public void Delete(int id)
        {
            _orderRepo.DeleteOrder(id);
        }

        public NewOrderVm Get(int id)
        {
            var order = _orderRepo.GetOrderById(id);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public List<NewOrderVm> GetAll()
        {
            var orders = _orderRepo.GetAllOrders()
                            .ProjectTo<NewOrderVm>(_mapper.ConfigurationProvider)
                            .ToList();
            return orders;
        }

        public List<NewOrderVm> GetAll(string searchName)
        {
            var orders = _orderRepo.GetAllOrders().Where(o => o.Number.ToString().StartsWith(searchName))
                            .ProjectTo<NewOrderVm>(_mapper.ConfigurationProvider)
                            .ToList();
            return orders;
        }

        public void Update(NewOrderVm objectVm)
        {
            var order = _mapper.Map<Order>(objectVm);
            _orderRepo.UpdatedOrder(order);
        }

        public abstract int AddOrder(NewOrderVm model);
        public abstract int AddPayment(NewPaymentVm paymentVm);
        public abstract int AddRefund(NewRefundVm refundVm);
        public abstract void DeleteOrder(int id);
        public abstract void DeletePayment(int id);
        public abstract void DeleteRefund(int id);
        public abstract IQueryable<Item> GetAllItemsToOrder();
        public abstract IQueryable<NewOrderItemVm> GetAllItemsOrderedForAdd();
        public abstract ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString);
        public abstract List<OrderItemForListVm> GetAllItemsOrdered();
        public abstract ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        public abstract List<OrderItemForListVm> GetAllItemsOrderedByItemId(int id);
        public abstract ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString);
        public abstract List<OrderForListVm> GetAllOrders();
        public abstract ListForPaymentVm GetAllPayments(int pageSize, int pageNo, string searchString);
        public abstract List<PaymentForListVm> GetAllPayments();
        public abstract ListForRefundVm GetAllRefunds(int pageSize, int pageNo, string searchString);
        public abstract List<RefundForListVm> GetAllRefunds();
        public abstract OrderDetailsVm GetOrderDetail(int id);
        public abstract NewOrderVm GetOrderForEdit(int id);
        public abstract PaymentDetailsVm GetPaymentDetail(int id);
        public abstract NewPaymentVm GetPaymentForEdit(int id);
        public abstract RefundDetailsVm GetRefundDetail(int id);
        public abstract NewRefundVm GetRefundForEdit(int id);
        public abstract void UpdateOrder(NewOrderVm orderVm);
        public abstract void UpdatePayment(NewPaymentVm paymentVm);
        public abstract void UpdateRefund(NewRefundVm refundVm);
        public abstract IQueryable<NewCustomerForOrdersVm> GetAllCustomers();
        public abstract IQueryable<NewCouponVm> GetAllCoupons();
        public abstract int CheckPromoCode(string code);
        public abstract int UpdateCoupon(int couponId, NewOrderVm order);
        public abstract int AddCouponUsed(NewCouponUsedVm couponUsedVm);
        public abstract NewOrderVm GetOrderById(int orderId);
        public abstract void CalculateCost(NewOrderVm order, NewOrderItemVm model);
        public abstract NewCustomerForOrdersVm GetCustomerById(int id);
        public abstract void AddOrderItems(List<NewOrderItemVm> orderItemsVm);
        public abstract NewPaymentVm GetPaymentById(int id);
        public abstract OrderItemDetailsVm GetOrderItemDetail(int id);
        public abstract bool CheckEnteredRefund(string reasonRefund);
        public abstract ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo);
        public abstract List<OrderForListVm> GetAllOrdersByCustomerId(int customerId);
        public abstract IQueryable<NewCustomerForOrdersVm> GetCustomersByUserId(string userId);
        public abstract ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo);
        public abstract List<OrderForListVm> GetAllOrdersByUserId(string userId);
        public abstract int AddOrderItem(NewOrderItemVm model);
        public abstract List<ItemsAddToCartVm> GetItemsAddToCart();
        public abstract ListForItemOrderVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        public abstract List<NewOrderItemVm> GetOrderItemsNotOrderedByUserId(string userId);
        public abstract int AddCustomer(NewCustomerVm newCustomer);
        public abstract void UpdateOrderItems(List<NewOrderItemVm> orderItemsVm);
        public abstract int OrderItemCount(string userId);
        public abstract void UpdateOrderItem(OrderItemForListVm orderItemVm);
        public abstract int AddItemToOrderItem(int itemId, string userId);
        public abstract void DeleteOrderItem(int id);
        public abstract bool CheckIfOrderExists(int id);
        public abstract bool CheckIfPaymentExists(int id);
        public abstract bool CheckIfRefundExists(int id);
        public abstract bool CheckIfOrderItemExists(int id);
        public abstract NewPaymentVm InitPayment(int orderId);
        public abstract T MapTo<T, U>(U model) where T : BaseVm where U : BaseVm;
        public abstract List<T> MapToList<T, U>(List<U> model) where T : BaseVm where U : BaseVm;
    }
}
