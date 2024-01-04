﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services.Orders
{
    public class OrderService : AbstractService<OrderVm, IOrderRepository, Order>, IOrderService
    {
        private readonly IOrderItemService _orderItemService;
        private readonly IItemService _itemService;
        private readonly ICouponService _couponService;
        private readonly ICouponUsedRepository _couponUsedRepository;
        private readonly ICustomerService _customerService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderService(IOrderRepository orderRepo, IMapper mapper, IOrderItemService orderItemService, IItemService itemService, ICouponService couponService, ICouponUsedRepository couponUsedRepository, ICustomerService customerService,
                IHttpContextAccessor httpContextAccessor) : base(orderRepo, mapper)
        {
            _orderItemService = orderItemService;
            _itemService = itemService;
            _couponService = couponService;
            _couponUsedRepository = couponUsedRepository;
            _customerService = customerService;
            _httpContextAccessor = httpContextAccessor;
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
            if (vm is null)
            {
                throw new BusinessException($"{typeof(OrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            var id = _repo.Add(order);
            return id;
        }

        public override void Delete(OrderVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(OrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            _repo.Delete(order);
        }

        public override void Update(OrderVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(OrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            _repo.Update(order);
        }

        public void Update(NewOrderVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(NewOrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            _repo.Update(order);
        }

        public int AddOrder(AddOrderDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(AddOrderDto).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var dto = model.AsDto();
            dto.CurrencyId = 1;
            dto.UserId = _httpContextAccessor.GetUserId();
            dto.OrderItems.ForEach(oi => oi.UserId = dto.UserId);
            return AddOrder(dto);
        }

        public bool DeleteOrder(int id)
        {
            return Delete(id);
        }

        public void DeleteRefundFromOrder(int id)
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

        public void DeleteCouponUsedFromOrder(int orderId, int couponUsedId)
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

            var coupon = _couponService.GetCouponFirstOrDefault(c => c.CouponUsedId == couponUsedId);

            if (coupon is null)
            {
                throw new BusinessException("Given invalid couponUsedId");
            }

            order.Cost = order.Cost / (1 - (decimal)coupon.Discount / 100);
            //_repo.Update(order);
        }

        public ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _repo.GetAllOrders().Where(o => o.Number.ToString().StartsWith(searchString))
                            .Include(c => c.Currency)
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

        public void UpdateOrder(OrderDto orderDto)
        {
            ValidateOrder(orderDto);
            var order = _repo.GetAllOrders()
                             .Where(o => o.Id == orderDto.Id)
                             .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                             .AsNoTracking()
                             .FirstOrDefault();
            orderDto.Number = order.Number;
            orderDto.Ordered = order.Ordered;
            orderDto.Cost = 0;
            var orderItems = order.OrderItems;
            CheckOrderItemsOrderByUser(orderDto, orderItems);
            AddNewOrderItemsIfNotExistAndCalculateCost(orderDto);
            orderItems = order.OrderItems;
            CalculateCost(orderDto, orderItems);
            var orderToUpdate = _mapper.Map<Order>(orderDto);
            _repo.UpdatedOrder(orderToUpdate);
        }

        private static void ValidateOrder(OrderDto orderDto)
        {
            if (orderDto is null)
            {
                throw new BusinessException($"{typeof(OrderDto).Name} cannot be null");
            }

            if (orderDto.OrderItems is null || orderDto.OrderItems.Count == 0)
            {
                throw new BusinessException("Items shouldnt be empty");
            }
        }

        private static void CalculateCost(OrderDto orderDto, ICollection<OrderItem> itemsFromDb)
        {
            var orderCost = decimal.Zero;
            foreach (var orderItem in orderDto.OrderItems ?? new List<OrderItemDto>())
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();

                if (item != null)
                {
                    orderItem.ItemOrderQuantity = item.ItemOrderQuantity;
                    orderItem.ItemId = item.ItemId;
                    orderItem.CouponUsedId = item.CouponUsedId;

                    orderCost += item.Item.Cost * orderItem.ItemOrderQuantity;
                }
            }
            orderDto.Cost += orderCost;
        }

        private void AddNewOrderItemsIfNotExistAndCalculateCost(OrderDto orderDto)
        {
            var orderItemsToAdd = orderDto.OrderItems.Where(oi => oi.Id == 0).ToList();

            if (orderItemsToAdd.Count == 0)
            {
                return;
            }

            var ids = orderItemsToAdd.Select(i => i.ItemId);
            var items = (from id in ids
                         join item in _itemService.GetItems()
                         on id equals item.Id
                         select item).AsQueryable().AsNoTracking().ToList();

            foreach (var orderItem in orderItemsToAdd)
            {
                var orderItemVm = new OrderItemDto { Id = 0, ItemId = orderItem.ItemId, ItemOrderQuantity = orderItem.ItemOrderQuantity, UserId = orderDto.UserId, OrderId = orderDto.Id };
                var orderItemId = _orderItemService.AddOrderItem(orderItemVm);
                orderItem.Id = orderItemId;
            }

            var orderItems = _mapper.Map<List<OrderItem>>(orderItemsToAdd);
            orderItems.ForEach(oi => oi.Item = _mapper.Map<Item>(items.Where(i => i.Id == oi.ItemId).FirstOrDefault()));
            CalculateCost(orderDto, orderItems);
        }

        private static void CheckOrderItemsOrderByUser(OrderDto orderVm, ICollection<OrderItem> itemsFromDb)
        {
            StringBuilder errors = new();

            foreach (var orderItem in orderVm.OrderItems ?? new List<OrderItemDto>())
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();

                if (item != null)
                {
                    if (orderItem.UserId != item.UserId)
                    {
                        errors.AppendLine($"This item {orderItem.Id} is not ordered by current user");
                        continue;
                    }
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
        }

        public void AddCouponUsedToOrder(int orderId, int couponUsedId)
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

            var coupon = _couponService.GetCouponFirstOrDefault(c => c.CouponUsedId == couponUsedId);

            if (coupon is null)
            {
                throw new BusinessException("Given invalid couponUsedId");
            }

            orderVm.CouponUsedId = couponUsedId;
            foreach (var orderItem in orderVm.OrderItems)
            {
                orderItem.CouponUsedId = couponUsedId;
            }

            orderVm.Cost = (1 - (decimal)coupon.Discount / 100) * order.Cost;
            Update(orderVm);
        }

        public int AddCouponToOrder(int couponId, NewOrderVm order)
        {
            if (order is null)
            {
                throw new BusinessException($"{typeof(NewOrderVm).Name} cannot be null");
            }

            var coupon = _couponService.GetCoupon(couponId);

            if (coupon == null)
            {
                throw new BusinessException($"Coupon with id {couponId} doesnt exists");
            }

            var couponUsed = new CouponUsed()
            {
                Id = 0,
                CouponId = couponId,
                OrderId = order.Id
            };
            var couponUsedId = _couponUsedRepository.AddCouponUsed(couponUsed);
            coupon.CouponUsedId = couponUsedId;
            _couponService.UpdateCoupon(coupon);
            order.Cost = (1 - (decimal)coupon.Discount / 100) * order.Cost;
            order.CouponUsedId = couponUsedId;
            if (order.OrderItems.Count > 0)
            {
                order.OrderItems.ForEach(oi =>
                {
                    oi.CouponUsedId = couponUsedId;
                });
            }
            Update(order);
            return couponUsedId;
        }

        public OrderDto GetOrderById(int orderId)
        {
            var order = _repo.GetOrderById(orderId);
            var orderVm = _mapper.Map<OrderDto>(order);
            return orderVm;
        }

        public NewOrderVm GetOrderForRealization(int orderId)
        {
            var order = _repo.GetOrderById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _repo.GetAllOrders().Where(o => o.CustomerId == customerId)
                            .Include(c => c.Currency)
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

        public IQueryable<CustomerInformationForOrdersVm> GetCustomersByUserId(string userId)
        {
            var customers = _customerService.GetCustomersInformationByUserId(userId);
            return customers;
        }

        public ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _repo.GetAllOrders().Where(o => o.UserId == userId)
                            .Include(c => c.Currency)
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

        public List<OrderForListVm> GetAllOrders(Expression<Func<Order, bool>> expression)
        {
            return _repo.GetAllOrders().Where(expression)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public void AddRefundToOrder(int orderId, int refundId)
        {
            var order = _repo.GetAll()
                .Include(oi => oi.OrderItems)
                .Where(o => o.Id == orderId && o.IsPaid && o.IsDelivered).FirstOrDefault();

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

        public ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _repo.GetAll().Where(o => o.IsPaid == true && o.IsDelivered == false)
                            .Where(o => o.Number.ToString().StartsWith(searchString))
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider);

            var ordersToShow = orders.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Orders = ordersToShow,
                Count = orders.Count()
            };

            return ordersList;
        }

        private static void ValidatePageSizeAndPageNo(int pageSize, int pageNo)
        {
            if (pageSize <= 0)
            {
                throw new BusinessException("Page size should be positive and greater than 0");
            }

            if (pageNo <= 0)
            {
                throw new BusinessException("Page number should be positive and greater than 0");
            }
        }

        public void DispatchOrder(int orderId)
        {
            var order = _repo.GetAll().Where(o => o.Id == orderId).FirstOrDefault(o => o.IsDelivered == false && o.IsPaid == true)
                ?? throw new BusinessException($"Order with id {orderId} not found, check your order if is not delivered and is paid");
            order.IsDelivered = true;
            order.Delivered = DateTime.Now;
            _repo.Update(order);
        }

        public void UpdateOrderWithExistedOrderItemsIds(OrderDto orderDto)
        {
            ValidateOrder(orderDto);
            AddExistedOrderItemsToOrder(orderDto);
            var order = _repo.GetAllOrders().Where(o => o.Id == orderDto.Id)
                .Include(inc => inc.OrderItems)
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .FirstOrDefault();
            orderDto.Number = order.Number;
            orderDto.Ordered = order.Ordered;
            orderDto.Cost = 0;
            var orderItems = order.OrderItems;
            CheckOrderItemsOrderByUser(orderDto, orderItems);
            orderItems = order.OrderItems;
            CalculateCost(orderDto, orderItems);
            order.Cost = orderDto.Cost;
            _repo.UpdatedOrder(order);
        }

        private void AddExistedOrderItemsToOrder(OrderDto orderDto)
        {
            var order = _repo.GetAllOrders().Where(o => o.Id == orderDto.Id)
                .Include(inc => inc.OrderItems).FirstOrDefault();

            var ids = orderDto.OrderItems.Where(oi => oi.Id > 0 && oi.OrderId == null).Select(i => i.Id);
            var items = (from id in ids
                         join orderItem in _orderItemService.GetOrderItems()
                         on id equals orderItem.Id
                         select orderItem).AsQueryable().AsNoTracking().ToList();

            var errors = new StringBuilder();
            foreach (var orderItem in items)
            {
                if (orderItem.UserId != orderDto.UserId)
                {
                    errors.AppendLine($"Order item with id: '{orderItem.Id}' is not order by this user");
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            foreach (var orderItem in items)
            {
                orderItem.OrderId = orderDto.Id;
                order.OrderItems.Add(orderItem);

                var oi = orderDto.OrderItems.Where(oi => oi.Id == orderItem.Id).FirstOrDefault();
                if (oi != null)
                {
                    orderDto.OrderItems.Remove(oi);
                    orderDto.OrderItems.Add(_mapper.Map<OrderItemDto>(orderItem));
                }
            }

            _repo.Update(order);
        }

        public OrderDto GetOrderByIdReadOnly(int id)
        {
            var order = _repo.GetByIdReadOnly(id);
            var orderVm = _mapper.Map<OrderDto>(order);
            return orderVm;
        }

        public int GetOrderNumber(int orderId)
        {
            return _repo.GetAll()
                .Where(o => o.Id == orderId)
                .Select(o => o.Number)
                .FirstOrDefault();
        }

        public int AddOrderFromCart(AddOrderFromCartDto model)
        {
            var userId = _httpContextAccessor.GetUserId();
            var dto = new OrderDto
            {
                CustomerId = model.CustomerId,
                UserId = userId,
                OrderItems = _orderItemService.GetOrderItemsForRealization(userId).ToList(),
                CurrencyId = 1
            };

            var id = AddOrder(dto);
            dto.OrderItems.ForEach(oi => oi.OrderId = id);
            _orderItemService.UpdateOrderItems(dto.OrderItems);
            return id;
        }

        private int AddOrder(OrderDto dto)
        {
            Random random = new();

            if (dto.Number == 0)
            {
                dto.Number = random.Next(100, 10000);
            }

            var dateNotSet = new DateTime();

            if (dto.Ordered == dateNotSet)
            {
                dto.Ordered = DateTime.Now;
            }

            var ids = dto.OrderItems?.Select(oi => oi.Id)?.ToList() ?? new List<int>();
            var orderItemsQueryable = _repo.GetAllOrderItems();
            var orderItems = (from orderItemId in ids
                              join orderItem in orderItemsQueryable
                                 on orderItemId equals orderItem.Id
                              select orderItem).AsQueryable().Include(i => i.Item).AsNoTracking().ToList();

            CheckOrderItemsOrderByUser(dto, orderItems);
            CalculateCost(dto, orderItems);

            var order = _mapper.Map<Order>(dto);
            var id = _repo.AddOrder(order);

            return id;
        }
    }
}
