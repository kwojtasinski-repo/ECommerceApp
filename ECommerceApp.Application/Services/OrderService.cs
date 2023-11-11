using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services.Coupons;
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
        private readonly ICouponService _couponService;
        private readonly ICouponUsedRepository _couponUsedRepository;
        private readonly ICustomerService _customerService;

        public OrderService(IOrderRepository orderRepo, IMapper mapper, IOrderItemService orderItemService, IItemService itemService, ICouponService couponService, ICouponUsedRepository couponUsedRepository, ICustomerService customerService) : base(orderRepo, mapper)
        {
            _orderItemService = orderItemService;
            _itemService = itemService;
            _couponService = couponService;
            _couponUsedRepository = couponUsedRepository;
            _customerService = customerService;
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

        public int AddOrder(OrderVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(OrderVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            model.CurrencyId = 1;
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

            var ids = model.OrderItems?.Select(oi => oi.Id)?.ToList() ?? new List<int>();
            var orderItemsQueryable = _repo.GetAllOrderItems();
            var orderItems = (from orderItemId in ids
                             join orderItem in orderItemsQueryable
                                on orderItemId equals orderItem.Id
                             select orderItem).AsQueryable().Include(i => i.Item).AsNoTracking().ToList();

            CheckOrderItemsOrderByUser(model, orderItems);
            CalculateCost(model, orderItems);

            var order = _mapper.Map<Order>(model);
            var id = _repo.AddOrder(order);

            return id;
        }

        public void DeleteOrder(int id)
        {
            Delete(id);
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

            order.Cost = order.Cost / ((1 - (decimal)coupon.Discount / 100));
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

        public void UpdateOrder(OrderVm orderVm)
        {
            ValidateOrder(orderVm);
            var order = _repo.GetAllOrders().Where(o => o.Id == orderVm.Id).Include(inc => inc.OrderItems).AsNoTracking().FirstOrDefault();
            orderVm.Number = order.Number;
            orderVm.Ordered = order.Ordered;
            orderVm.Cost = 0;
            var orderItems = order.OrderItems;
            CheckOrderItemsOrderByUser(orderVm, orderItems);
            AddNewOrderItemsIfNotExistAndCalculateCost(orderVm);
            orderItems = order.OrderItems;
            CalculateCost(orderVm, orderItems);
            var orderToUpdate = _mapper.Map<Order>(orderVm);
            _repo.UpdatedOrder(orderToUpdate);
        }

        private void ValidateOrder(OrderVm orderVm)
        {
            if (orderVm is null)
            {
                throw new BusinessException($"{typeof(OrderVm).Name} cannot be null");
            }

            if (orderVm.OrderItems is null)
            {
                throw new BusinessException("Items shouldnt be empty");
            }

            if (orderVm.OrderItems.Count == 0)
            {
                throw new BusinessException("Items shouldnt be empty");
            }
        }

        private void CalculateCost(OrderVm orderVm, ICollection<OrderItem> itemsFromDb)
        {
            var orderCost = decimal.Zero;
            foreach (var orderItem in orderVm.OrderItems ?? new List<OrderItemVm>())
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
            orderVm.Cost += orderCost;
        }

        private void AddNewOrderItemsIfNotExistAndCalculateCost(OrderVm orderVm)
        {
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

            foreach(var orderItem in orderItemsToAdd)
            {
                var orderItemVm = new OrderItemVm { Id = 0, ItemId = orderItem.ItemId, ItemOrderQuantity = orderItem.ItemOrderQuantity, UserId = orderVm.UserId, OrderId = orderVm.Id };
                var orderItemId = _orderItemService.AddOrderItem(orderItemVm);
                orderItem.Id = orderItemId;
            }

            var orderItems = _mapper.Map<List<OrderItem>>(orderItemsToAdd);
            orderItems.ForEach(oi => oi.Item = _mapper.Map<Item>(items.Where(i => i.Id == oi.ItemId).FirstOrDefault()));
            CalculateCost(orderVm, orderItems);
        }

        private void CheckOrderItemsOrderByUser(OrderVm orderVm, ICollection<OrderItem> itemsFromDb)
        {
            StringBuilder errors = new StringBuilder();

            foreach(var orderItem in orderVm.OrderItems ?? new List<OrderItemVm>())
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();

                if(item != null)
                {
                    if(orderItem.UserId != item.UserId)
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
            foreach(var orderItem in orderVm.OrderItems)
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
            order.Cost = (1 - (decimal)coupon.Discount/100) * order.Cost;
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

        private void ValidatePageSizeAndPageNo(int pageSize, int pageNo)
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

        public void UpdateOrderWithExistedOrderItemsIds(OrderVm orderVm)
        {
            ValidateOrder(orderVm);
            AddExistedOrderItemsToOrder(orderVm);
            var order = _repo.GetAllOrders().Where(o => o.Id == orderVm.Id)
                .Include(inc => inc.OrderItems)
                .Include(inc => inc.OrderItems).ThenInclude(inc => inc.Item)
                .FirstOrDefault();
            orderVm.Number = order.Number;
            orderVm.Ordered = order.Ordered;
            orderVm.Cost = 0;
            var orderItems = order.OrderItems;
            CheckOrderItemsOrderByUser(orderVm, orderItems);
            orderItems = order.OrderItems;
            CalculateCost(orderVm, orderItems);
            order.Cost = orderVm.Cost;
            _repo.UpdatedOrder(order);
        }

        private void AddExistedOrderItemsToOrder(OrderVm orderVm)
        {
            var order = _repo.GetAllOrders().Where(o => o.Id == orderVm.Id)
                .Include(inc => inc.OrderItems).FirstOrDefault();

            var ids = orderVm.OrderItems.Where(oi => oi.Id > 0 && oi.OrderId == null).Select(i => i.Id);
            var items = (from id in ids
                         join orderItem in _orderItemService.GetOrderItems()
                         on id equals orderItem.Id
                         select orderItem).AsQueryable().AsNoTracking().ToList();

            var errors = new StringBuilder();
            foreach(var orderItem in items)
            {
                if (orderItem.UserId != orderVm.UserId)
                {
                    errors.AppendLine($"Order item with id: '{orderItem.Id}' is not order by this user");
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            order.OrderItems = new List<OrderItem>();
            foreach (var orderItem in items)
            {
                orderItem.OrderId = orderVm.Id;
                order.OrderItems.Add(orderItem);

                var oi = orderVm.OrderItems.Where(oi => oi.Id == orderItem.Id).FirstOrDefault();
                if (oi != null)
                {
                    orderVm.OrderItems.Remove(oi);
                    orderVm.OrderItems.Add(_mapper.Map<OrderItemVm>(orderItem));
                }
            }

            _repo.Update(order);
        }

        public OrderVm GetOrderByIdReadOnly(int id)
        {
            var order = _repo.GetByIdReadOnly(id);
            var orderVm = _mapper.Map<OrderVm>(order);
            return orderVm;
        }
    }
}
