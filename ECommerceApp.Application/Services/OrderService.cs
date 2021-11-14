using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class OrderService : AbstractService<OrderVm, IOrderRepository, Order>, IOrderService
    {
        public OrderService(IOrderRepository orderRepo, IMapper mapper) : base(orderRepo, mapper)
        {
        }

        public override OrderVm Get(int id)
        {
            var order = _repo.GetById(id);
            if (order != null)
            {
                _repo.DetachEntity(order);
            }
            var orderVm = new OrderVm().MapToOrderVm(order);
            return orderVm;
        }

        public override int Add(OrderVm vm)
        {
            var order = vm.MapToOrder();
            var id = _repo.Add(order);
            return id;
        }

        public override void Delete(OrderVm vm)
        {
            var order = vm.MapToOrder();
            _repo.Delete(order);
        }

        public override void Update(OrderVm vm)
        {
            var order = vm.MapToOrder();
            _repo.Update(order);
        }

        public void Update(NewOrderVm vm)
        {
            var order = _mapper.Map<Order>(vm);
            _repo.Update(order);
        }

        public int AddOrder(NewOrderVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            Random random = new Random();
            if (model.Number == 0)
            {
                model.Number = random.Next(100, 10000);
            }
            var dateNotSet = new DateTime();
            if (model.Ordered == dateNotSet)
            {
                model.Ordered = DateTime.Now;
            }
            CheckOrderItemsOrderByUser(model);

            var order = _mapper.Map<Order>(model);
            var id = _repo.AddOrder(order);

            return id;
        }

        public int AddRefund(RefundVm refundVm)
        {
            if (refundVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (refundVm.RefundDate == new DateTime())
            {
                refundVm.RefundDate = DateTime.Now;
            }

            if (refundVm.CustomerId == 0)
            {
                var customerId = _repo.GetAllOrders().Where(o => o.Id == refundVm.OrderId).Include(c => c.Customer).Select(or => or.CustomerId).FirstOrDefault();
                if (customerId == 0)
                {
                    throw new BusinessException($"There is no order with id = {refundVm.OrderId}");
                }
                refundVm.CustomerId = customerId;
            }

            var refund = _mapper.Map<Refund>(refundVm);
            var id = _repo.AddRefund(refund);
            return id;
        }

        public void DeleteOrder(int id)
        {
            Delete(id);
        }

        public void DeleteRefund(int id)
        {
            _repo.DeleteRefund(id);
        }

        public IQueryable<Item> GetAllItemsToOrder()
        {
            var items = _repo.GetAllItems();
            return items;
        }

        public void DeleteCouponUsed(int orderId, int couponUsedId)
        {
            var order = _repo.GetAll().Include(oi => oi.OrderItems).Where(o => o.Id == orderId).FirstOrDefault();

            if (order is null)
            {
                throw new BusinessException("Given invalid id");
            }

            if (order.IsPaid)
            {
                throw new BusinessException("Cannot delete coupon when order is paid");
            }

            //_repo.DetachEntity(order);
            //_repo.DetachEntity(order.OrderItems);

            order.CouponUsedId = null;
            foreach (var orderItem in order.OrderItems)
            {
                orderItem.CouponUsedId = null;
            }

            //_repo.Update(order);
        }

        public IQueryable<NewOrderItemVm> GetAllItemsOrderedForAdd()
        {
            var itemOrders = _repo.GetAllOrderItems()
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider);

            return itemOrders;
        }

        public ListForItemOrderVm GetAllItemsOrdered(int pageSize, int pageNo, string searchString)
        {
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.Item.Name.StartsWith(searchString) ||
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

        public List<OrderItemForListVm> GetAllItemsOrdered()
        {
            var itemOrder = _repo.GetAllOrderItems()
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            
            return itemOrder;
        }

        public ListForItemOrderVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo)
        {
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.ItemId == id)
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

        public List<OrderItemForListVm> GetAllItemsOrderedByItemId(int id)
        {
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.ItemId == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            
            return itemOrder;
        }

        public ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            var orders = _repo.GetAllOrders().Where(o => o.Number.ToString().StartsWith(searchString))
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

        public List<OrderForListVm> GetAllOrders()
        {
            return _repo.GetAllOrders()
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public ListForRefundVm GetAllRefunds(int pageSize, int pageNo, string searchString)
        {
            var refunds = _repo.GetAllRefunds().Where(r => r.Reason.StartsWith(searchString) 
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

        public List<RefundForListVm> GetAllRefunds()
        {
            var refunds = _repo.GetAllRefunds()
                            .ProjectTo<RefundForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return refunds;
        }

        public OrderDetailsVm GetOrderDetail(int id)
        {
            var orderDetails = _repo.GetOrderById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        public NewOrderVm GetOrderForEdit(int id)
        {
            var order = _repo.GetOrderById(id);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public RefundDetailsVm GetRefundDetail(int id)
        {
            var refundDetails = _repo.GetRefundById(id);
            var refundDetailsVm = _mapper.Map<RefundDetailsVm>(refundDetails);
            return refundDetailsVm;
        }

        public RefundVm GetRefundForEdit(int id)
        {
            var refund = _repo.GetRefundById(id);
            var refundVm = _mapper.Map<RefundVm>(refund);
            return refundVm;
        }

        public void UpdateOrder(NewOrderVm orderVm)
        {
            var order = _repo.GetAllOrders().Where(o => o.Id == orderVm.Id).AsNoTracking().FirstOrDefault();
            orderVm.Number = order.Number;
            orderVm.Ordered = order.Ordered;
            orderVm.Cost = 0;
            CheckOrderItemsOrderByUser(orderVm);
            var orderToUpdate = _mapper.Map<Order>(orderVm);
            _repo.UpdatedOrder(orderToUpdate);
        }

        private void CheckOrderItemsOrderByUser(NewOrderVm orderVm)
        {
            var ids = orderVm.OrderItems.Select(oi => oi.Id).ToList();
            var orderItemsQueryable = _repo.GetAllOrderItems();
            var itemsFromDb = (from id in ids
                               join orderItem in orderItemsQueryable
                                   on id equals orderItem.Id
                               select orderItem).AsQueryable().Include(i => i.Item).AsNoTracking().ToList();

            StringBuilder errors = new StringBuilder();

            foreach(var orderItem in orderVm.OrderItems)
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();

                if(item != null)
                {
                    if(orderItem.UserId != item.UserId)
                    {
                        errors.AppendLine($"This item {orderItem.Id} is not ordered by current user");
                        continue;
                    }

                    orderItem.ItemOrderQuantity = item.ItemOrderQuantity;
                    orderItem.ItemId = item.ItemId;
                    orderItem.CouponUsedId = item.CouponUsedId;

                    //orderVm.Cost += item.Item.Cost * orderItem.ItemOrderQuantity; // JS liczy koszta
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
        }

        public void UpdateRefund(RefundVm refundVm)
        {
            var refund = _mapper.Map<Refund>(refundVm);
            _repo.UpdateRefund(refund);
        }

        public void AddCouponToOrder(int orderId, int couponUsedId)
        {
            var order = _repo.GetAll().Include(oi => oi.OrderItems).Where(o => o.Id == orderId).FirstOrDefault();

            if (order is null)
            {
                throw new BusinessException("Cannot add coupon if order not exists");
            }

            _repo.DetachEntity(order); // detach before update
            _repo.DetachEntity(order.OrderItems);

            var orderVm = _mapper.Map<NewOrderVm>(order);

            if (orderVm.IsPaid)
            {
                throw new BusinessException("Cannot add coupon to paid order");
            }

            orderVm.CouponUsedId = couponUsedId;

            foreach(var orderItem in orderVm.OrderItems)
            {
                orderItem.CouponUsedId = couponUsedId;
            }

            Update(orderVm);
        }

        public IQueryable<NewCustomerForOrdersVm> GetAllCustomers()
        {
            var customers = _repo.GetAllCustomers();
            var customersVm = customers.ProjectTo<NewCustomerForOrdersVm>(_mapper.ConfigurationProvider);
            return customersVm;
        }

        public IQueryable<CouponVm> GetAllCoupons()
        {
            var coupons = _repo.GetAllCoupons();
            var couponsVm = coupons.ProjectTo<CouponVm>(_mapper.ConfigurationProvider);
            return couponsVm;
        }

        public int CheckPromoCode(string code)
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

        public int UpdateCoupon(int couponId, NewOrderVm order)
        {
            var couponUsed = new CouponUsedVm()
            {
                Id = 0,
                CouponId = couponId,
                OrderId = order.Id
            };
            var couponUsedId = AddCouponUsed(couponUsed);
            var coupon = _repo.GetCouponById(couponId);
            _repo.UpdateCoupon(coupon, couponUsedId);
            order.Cost = (1 - (decimal)coupon.Discount/100) * order.Cost;
            return couponUsedId;
        }

        public int AddCouponUsed(CouponUsedVm couponUsedVm)
        {
            if (couponUsedVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            var id = _repo.AddCouponUsed(couponUsed);
            return id;
        }

        public NewOrderVm GetOrderById(int orderId)
        {
            var order = _repo.GetOrderById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public void CalculateCost(NewOrderVm order, NewOrderItemVm model)
        {
            throw new NotImplementedException();
        }

        public NewCustomerForOrdersVm GetCustomerById(int id)
        {
            var customer = _repo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerForOrdersVm>(customer);
            return customerVm;
        }

        public void AddOrderItems(List<NewOrderItemVm> orderItemsVm)
        {
            var orderItems = _mapper.Map<List<NewOrderItemVm>, List<OrderItem>>(orderItemsVm);
            _repo.AddOrderItems(orderItems);
        }

        public OrderItemDetailsVm GetOrderItemDetail(int id)
        {
            var orderItem = _repo.GetAllOrderItems().Include(i => i.Item).Where(oi => oi.Id == id).FirstOrDefault();
            var orderItemVm = _mapper.Map<OrderItemDetailsVm>(orderItem);
            return orderItemVm;
        }

        public bool CheckEnteredRefund(string reasonRefund)
        {
            var refunds = _repo.GetAllRefunds().ToList();
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

        public ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo)
        {
            var orders = _repo.GetAllOrders().Where(o => o.CustomerId == customerId)
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

        public List<OrderForListVm> GetAllOrdersByCustomerId(int customerId)
        {
            var orders = _repo.GetAllOrders().Where(o => o.CustomerId == customerId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return orders;
        }

        public IQueryable<NewCustomerForOrdersVm> GetCustomersByUserId(string userId)
        {
            var customers = _repo.GetCustomersByUserId(userId);
            var customersVm = customers.ProjectTo<NewCustomerForOrdersVm>(_mapper.ConfigurationProvider);
            return customersVm;
        }

        public ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo)
        {
            var orders = _repo.GetAllOrders().Where(o => o.UserId == userId)
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

        public List<OrderForListVm> GetAllOrdersByUserId(string userId)
        {
            var orders = _repo.GetAllOrders().Where(o => o.UserId == userId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return orders;
        }

        public int AddOrderItem(NewOrderItemVm model)
        {
            var orderItem = _mapper.Map<OrderItem>(model);
            var orderItemExist = GetOrderItem(orderItem);
            int id;
            if(orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _repo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                id = _repo.AddOrderItem(orderItem);
            }
            return id;
        }

        public List<ItemsAddToCartVm> GetItemsAddToCart()
        {
            var items = GetAllItemsToOrder();
            var itemsVm = items.ProjectTo<ItemsAddToCartVm>(_mapper.ConfigurationProvider).ToList();
            return itemsVm;
        }

        public ListForItemOrderVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
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

        public List<NewOrderItemVm> GetOrderItemsNotOrderedByUserId(string userId)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null).ToList();
            _repo.DetachEntity(itemOrders);
            var itemOrdersVm = _mapper.Map<List<NewOrderItemVm>>(itemOrders);

            return itemOrdersVm;
        }

        public int AddCustomer(NewCustomerVm newCustomer)
        {
            if (newCustomer.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customer = _mapper.Map<Customer>(newCustomer);
            var id = _repo.AddCustomer(customer);
            return id;
        }

        public void UpdateOrderItems(List<NewOrderItemVm> orderItemsVm)
        {
            var orderItems = _mapper.Map<List<NewOrderItemVm>, List<OrderItem>>(orderItemsVm);
            _repo.UpdateOrderItems(orderItems);
        }

        public int OrderItemCount(string userId)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<NewOrderItemVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return itemOrders.Count;
        }

        public void UpdateOrderItem(OrderItemForListVm orderItemVm)
        {
            var orderItem = _mapper.Map<OrderItemForListVm, OrderItem>(orderItemVm);
            _repo.UpdateOrderItem(orderItem);
        }

        public int AddItemToOrderItem(int itemId, string userId)
        {
            var orderItemExist = GetOrderItemByItemId(itemId, userId);
            int id;
            if (orderItemExist != null)
            {
                id = orderItemExist.Id;
                orderItemExist.ItemOrderQuantity += 1;
                _repo.UpdateOrderItem(orderItemExist);
            }
            else
            {
                var orderItem = CreateOrderItem(itemId, userId);
                id = _repo.AddOrderItem(orderItem);
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
            var item = _repo.GetOrderItemNotOrdered(orderItem);
            return item;
        }

        private OrderItem GetOrderItemByItemId(int itemId, string userId)
        {
            var orderItem = _repo.GetOrderItemNotOrderedByItemId(itemId, userId);
            return orderItem;
        }

        public void DeleteOrderItem(int id)
        {
            _repo.DeleteOrderItem(id);
        }

        public bool CheckIfOrderExists(int id)
        {
            var order = _repo.GetAllOrders().Where(o => o.Id == id).AsNoTracking().FirstOrDefault();
            if (order == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfPaymentExists(int id)
        {
            var payment = _repo.GetPaymentById(id);
            if (payment == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfRefundExists(int id)
        {
            var refund = _repo.GetRefundById(id);
            if (refund == null)
            {
                return false;
            }
            return true;
        }

        public bool CheckIfOrderItemExists(int id)
        {
            var orderItem = _repo.GetOrderItemById(id);
            if (orderItem == null)
            {
                return false;
            }
            return true;
        }

        public List<OrderItemForListVm> GetOrderItemsNotOrderedByUser(string userId)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return itemOrders;
        }
    }
}
