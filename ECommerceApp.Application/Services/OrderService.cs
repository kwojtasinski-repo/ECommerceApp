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
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class OrderService : AbstractService<OrderVm, IOrderRepository, Order>, IOrderService
    {
        private readonly IOrderItemService _orderItemService;
        private readonly IItemService _itemService;

        public OrderService(IOrderRepository orderRepo, IMapper mapper, IOrderItemService orderItemService, IItemService itemService) : base(orderRepo, mapper)
        {
            _orderItemService = orderItemService;
            _itemService = itemService;
        }

        public override OrderVm Get(int id)
        {
            var order = _repo.GetById(id);
            if (order != null)
            {
                _repo.DetachEntity(order);
            }
            var orderVm = _mapper.Map<OrderVm>(order);
            return orderVm;
        }

        public override int Add(OrderVm vm)
        {
            var order = _mapper.Map<Order>(vm);
            var id = _repo.Add(order);
            return id;
        }

        public override void Delete(OrderVm vm)
        {
            var order = _mapper.Map<Order>(vm);
            _repo.Delete(order);
        }

        public override void Update(OrderVm vm)
        {
            var order = _mapper.Map<Order>(vm);
            _repo.Update(order);
        }

        public void Update(NewOrderVm vm)
        {
            var order = _mapper.Map<Order>(vm);
            _repo.Update(order);
        }

        public int AddOrder(OrderVm model)
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

        public void DeleteOrder(int id)
        {
            Delete(id);
        }

        public void DeleteRefund(int id)
        {
            var orders = _repo.GetAll().Include(oi => oi.OrderItems).Where(r => r.RefundId == id).ToList();

            /*orders.ForEach(o =>
            {
                _repo.DetachEntity(o);
                _repo.DetachEntity(o.OrderItems);
            });*/

            orders.ForEach(o =>
            {
                o.RefundId = null;
                foreach (var oi in o.OrderItems)
                {
                    oi.RefundId = null;
                }
            });
            orders.ForEach(order => _repo.Update(order));
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

        public ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo)
        {
            var itemOrder = _repo.GetAllOrderItems().Where(oi => oi.ItemId == id)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
            var itemOrderToShow = itemOrder.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var itemOrderList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrderToShow,
                Count = itemOrder.Count
            };

            return itemOrderList;
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

        public OrderDetailsVm GetOrderDetail(int id)
        {
            var orderDetails = _repo.GetOrderById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        public void UpdateOrder(OrderVm orderVm)
        {
            var order = _repo.GetAllOrders().Where(o => o.Id == orderVm.Id).AsNoTracking().FirstOrDefault();
            orderVm.Number = order.Number;
            orderVm.Ordered = order.Ordered;
            orderVm.Cost = 0;
            CheckOrderItemsOrderByUser(orderVm);
            AddNewItemsIfExists(orderVm);
            var orderToUpdate = _mapper.Map<Order>(orderVm);
            _repo.UpdatedOrder(orderToUpdate);
        }

        private void AddNewItemsIfExists(OrderVm orderVm)
        {
            if (orderVm.OrderItems is null)
            {
                return;
            }

            if (orderVm.OrderItems.Count == 0)
            {
                return;
            }

            var orderItemsToAdd = orderVm.OrderItems.Where(oi => oi.Id == 0).ToList();

            if (orderItemsToAdd.Count == 0)
            {
                return;
            }
            var ids = orderItemsToAdd.Select(i => i.ItemId);
            var items = (from id in ids
                         join item in _itemService.GetItems()
                         on id equals item.Id
                         select item).AsQueryable().AsNoTracking().ToList();

            var cost = decimal.Zero;

            foreach(var orderItem in orderItemsToAdd)
            {
                var orderItemId = _orderItemService.AddOrderItem(new OrderItemVm { Id = 0, ItemId = orderItem.ItemId, ItemOrderQuantity = orderItem.ItemOrderQuantity, UserId = orderVm.UserId, OrderId = orderVm.Id });
                orderVm.OrderItems.Where(it => it.ItemId == orderItem.ItemId).FirstOrDefault().Id = orderItemId;
                var item = items.Where(it => it.Id == orderItem.ItemId).FirstOrDefault();
                cost += item.Cost * orderItem.ItemOrderQuantity;
            }

            orderVm.Cost += cost;
        }

        private void CheckOrderItemsOrderByUser(OrderVm orderVm)
        {
            if (orderVm.OrderItems is null)
            {
                return;
            }

            if (orderVm.OrderItems.Count == 0)
            {
                return;
            }

            var ids = orderVm.OrderItems.Select(oi => oi.Id).ToList();
            var orderItemsQueryable = _repo.GetAllOrderItems();
            var itemsFromDb = (from id in ids
                               join orderItem in orderItemsQueryable
                                   on id equals orderItem.Id
                               select orderItem).AsQueryable().Include(i => i.Item).AsNoTracking().ToList();
                        
            StringBuilder errors = new StringBuilder();

            var orderCost = decimal.Zero;
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

                    orderCost += item.Item.Cost * orderItem.ItemOrderQuantity; 
                }
            }
            orderVm.Cost = orderCost;

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
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

        public OrderVm GetOrderById(int orderId)
        {
            var order = _repo.GetOrderById(orderId);
            var orderVm = _mapper.Map<OrderVm>(order);
            return orderVm;
        }

        public NewOrderVm GetOrderForRealization(int orderId)
        {
            var order = _repo.GetOrderById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public NewCustomerForOrdersVm GetCustomerById(int id)
        {
            var customer = _repo.GetCustomerById(id);
            var customerVm = _mapper.Map<NewCustomerForOrdersVm>(customer);
            return customerVm;
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

        public ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo)
        {
            var itemOrders = _repo.GetAllOrderItems().Where(oi => oi.UserId == userId && oi.OrderId == null)
                            .ProjectTo<OrderItemForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            var itemOrdersToShow = itemOrders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderItemVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                ItemOrders = itemOrdersToShow,
                Count = itemOrders.Count
            };

            return ordersList;
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

        private OrderItem GetOrderItem(OrderItem orderItem)
        {
            var item = _repo.GetOrderItemNotOrdered(orderItem);
            return item;
        }

        public List<OrderForListVm> GetAllOrders(Expression<Func<Order, bool>> expression)
        {
            return _repo.GetAllOrders().Where(expression)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public void AddRefund(int orderId, int refundId)
        {
            var order = _repo.GetAll().Include(oi => oi.OrderItems).Where(o => o.Id == orderId).FirstOrDefault();
            
            if (order is null)
            {
                throw new BusinessException($"Order with id {orderId} not exists");
            }

            order.RefundId = refundId;
            foreach (var oi in order.OrderItems)
            {
                oi.RefundId = refundId;
            }
            _repo.Update(order);
        }
    }
}
