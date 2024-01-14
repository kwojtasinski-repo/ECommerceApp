using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
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
    internal class OrderService : IOrderService
    {
        private readonly IMapper _mapper;
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderItemService _orderItemService;
        private readonly IItemService _itemService;
        private readonly ICouponService _couponService;
        private readonly ICouponUsedRepository _couponUsedRepository;
        private readonly ICustomerService _customerService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPaymentHandler _paymentHandler;
        private readonly ICouponHandler _couponHandler;
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IItemRepository _itemRepository;

        public OrderService(IOrderRepository orderRepo, IMapper mapper, IOrderItemService orderItemService, IItemService itemService, ICouponService couponService, ICouponUsedRepository couponUsedRepository, ICustomerService customerService,
                IHttpContextAccessor httpContextAccessor, IPaymentHandler paymentHandler, ICouponHandler couponHandler,
                IOrderItemRepository orderItemRepository, IItemRepository itemRepository)
        {
            _orderRepository = orderRepo;
            _mapper = mapper;
            _orderItemService = orderItemService;
            _itemService = itemService;
            _couponService = couponService;
            _couponUsedRepository = couponUsedRepository;
            _customerService = customerService;
            _httpContextAccessor = httpContextAccessor;
            _paymentHandler = paymentHandler;
            _couponHandler = couponHandler;
            _orderItemRepository = orderItemRepository;
            _itemRepository = itemRepository;
        }

        public OrderDto Get(int id)
        {
            var order = _orderRepository.GetById(id);
            if (order != null)
            {
                _orderRepository.DetachEntity(order);
            }
            var orderVm = _mapper.Map<OrderDto>(order);
            return orderVm;
        }

        public int Add(OrderDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(OrderDto).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(dto);
            var id = _orderRepository.Add(order);
            return id;
        }

        public void Update(NewOrderVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(NewOrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            _orderRepository.Update(order);
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

            return AddOrderInternal(model);
        }

        public bool DeleteOrder(int id)
        {
            return _orderRepository.DeleteOrder(id);
        }

        public void DeleteRefundFromOrder(int id)
        {
            var orders = _orderRepository.GetAll().Include(oi => oi.OrderItems).Where(r => r.RefundId == id).ToList();

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
            orders.ForEach(order => _orderRepository.Update(order));
        }

        public void DeleteCouponUsedFromOrder(int orderId, int couponUsedId)
        {
            var order = _orderRepository.GetAll().Include(oi => oi.OrderItems).Where(o => o.Id == orderId).FirstOrDefault();

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

            var orders = _orderRepository.GetAllOrders().Where(o => o.Number.StartsWith(searchString))
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
            return _orderRepository.GetAllOrders()
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public OrderDetailsVm GetOrderDetail(int id)
        {
            var orderDetails = _orderRepository.GetOrderDetailsById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        private static void CalculateCost(Order order)
        {
            var cost = 0M;
            var discount = (1 - (order.CouponUsed?.Coupon.Discount / 100M) ?? 1);
            foreach (var orderItem in order.OrderItems ?? new List<OrderItem>())
            {
                cost += orderItem.Item.Cost * orderItem.ItemOrderQuantity * discount;
            }
            order.Cost = cost;
        }

        private static void CheckOrderItemsOrderByUser(Order order, ICollection<OrderItem> itemsFromDb)
        {
            CheckOrderItemsOrderByUser(order, itemsFromDb?.Select(oi => new OrderItemValidationModel(oi.Id, oi.UserId))
                    ?? new List<OrderItemValidationModel>());
        }

        private static void CheckOrderItemsOrderByUser(Order order, IEnumerable<OrderItemValidationModel> itemsFromDb)
        {
            StringBuilder errors = new();

            foreach (var orderItem in order.OrderItems ?? new List<OrderItem>())
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();
                if (item == default)
                {
                    continue;
                }

                if (orderItem.UserId != item.UserId)
                {
                    errors.AppendLine($"This item {orderItem.Id} is not ordered by current user");
                    continue;
                }
            }

            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
        }

        public void AddCouponUsedToOrder(int orderId, int couponUsedId)
        {
            var order = _orderRepository.GetAll().Include(oi => oi.OrderItems).Where(o => o.Id == orderId).FirstOrDefault()
                ?? throw new BusinessException("Cannot add coupon if order not exists");
            
            var coupon = _couponService.GetCouponFirstOrDefault(c => c.CouponUsedId == couponUsedId)
                ?? throw new BusinessException("Given invalid couponUsedId");

            UseCoupon(coupon, order);
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
            var order = _orderRepository.GetOrderById(orderId);
            var orderVm = _mapper.Map<OrderDto>(order);
            return orderVm;
        }

        public NewOrderVm GetOrderForRealization(int orderId)
        {
            var order = _orderRepository.GetOrderForRealizationById(orderId);
            if (order is null)
            {
                return null;
            }

            var orderVm = _mapper.Map<NewOrderVm>(order);
            // TODO: in future fetch items from frontend
            orderVm.Items = _itemService.GetAllItems(i => true).ToList();
            orderVm.UserId = _httpContextAccessor.GetUserId();
            return orderVm;
        }

        public NewOrderVm BuildVmForEdit(int orderId)
        {
            var order = _orderRepository.GetOrderDetailsById(orderId);
            if (order is null)
            {
                return null;
            }

            var orderVm = _mapper.Map<NewOrderVm>(order);
            // TODO: in future fetch items from frontend
            orderVm.Items = _itemService.GetAllItems(i => true).ToList();
            orderVm.UserId = _httpContextAccessor.GetUserId();
            return orderVm;
        }

        public ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _orderRepository.GetAllOrders().Where(o => o.CustomerId == customerId)
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
            var orders = _orderRepository.GetAllOrders().Where(o => o.CustomerId == customerId)
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

            var orders = _orderRepository.GetAllOrders().Where(o => o.UserId == userId)
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
            var orders = _orderRepository.GetAllOrders().Where(o => o.UserId == userId)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();

            return orders;
        }

        public List<OrderForListVm> GetAllOrders(Expression<Func<Order, bool>> expression)
        {
            return _orderRepository.GetAllOrders().Where(expression)
                            .ProjectTo<OrderForListVm>(_mapper.ConfigurationProvider)
                            .ToList();
        }

        public void AddRefundToOrder(int orderId, int refundId)
        {
            var order = _orderRepository.GetAll()
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
            _orderRepository.Update(order);
        }

        public ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _orderRepository.GetAll().Where(o => o.IsPaid == true && o.IsDelivered == false)
                            .Where(o => o.Number.StartsWith(searchString))
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
            var order = _orderRepository.GetAll().Where(o => o.Id == orderId).FirstOrDefault(o => o.IsDelivered == false && o.IsPaid == true)
                ?? throw new BusinessException($"Order with id {orderId} not found, check your order if is not delivered and is paid");
            order.IsDelivered = true;
            order.Delivered = DateTime.Now;
            _orderRepository.Update(order);
        }

        public OrderDto GetOrderByIdReadOnly(int id)
        {
            var order = _orderRepository.GetByIdReadOnly(id);
            var orderVm = _mapper.Map<OrderDto>(order);
            return orderVm;
        }

        public int AddOrderFromCart(AddOrderFromCartDto model)
        {
            var userId = _httpContextAccessor.GetUserId();
            var dto = new AddOrderDto
            {
                CustomerId = model.CustomerId,
                PromoCode = model.PromoCode,
                OrderItems = _orderItemService.GetOrderItemsIdsForRealization(_httpContextAccessor.GetUserId())
                    .Select(id => new OrderItemsIdsDto { Id = id }).ToList()
            };
            var id = AddOrderInternal(dto);
            return id;
        }

        private int AddOrderInternal(AddOrderDto model)
        {
            var dto = model.AsDto();
            dto.CurrencyId = 1;
            dto.UserId = _httpContextAccessor.GetUserId();
            dto.Number = Guid.NewGuid().ToString();

            var dateNotSet = new DateTime();
            if (dto.Ordered == dateNotSet)
            {
                dto.Ordered = DateTime.Now;
            }

            var ids = dto.OrderItems?.Select(oi => oi.Id)?.ToList() ?? new List<int>();
            dto.OrderItems = new List<OrderItemDto>();
            var order = _mapper.Map<Order>(dto);
            order.OrderItems = _orderItemRepository.GetOrderItemsToRealization(ids);
            CheckOrderItemsOrderByUser(order, order.OrderItems);
            CalculateCost(order);
            var id = _orderRepository.AddOrder(order);
            _couponHandler.HandleCouponChangesOnUpdateOrder(_couponService.GetCouponByCode(model.PromoCode), order, HandleCouponChangesDto.Of(model.PromoCode));
            if (order.CouponUsed is not null)
            {
                CalculateCost(order);
                _orderRepository.UpdatedOrder(order);
            }
            return id;
        }

        private void UseCoupon(CouponVm coupon, Order order)
        {
            if (order.IsPaid)
            {
                throw new BusinessException("Cannot add coupon to paid order");
            }

            var couponUsed = new CouponUsed()
            {
                Id = 0,
                CouponId = coupon.Id,
                OrderId = order.Id
            };

            order.Cost = (1 - (decimal)coupon.Discount / 100) * order.Cost;

            int couponUsedId;
            if (!coupon.CouponUsedId.HasValue)
            {
                couponUsedId = _couponUsedRepository.AddCouponUsed(couponUsed);
                coupon.CouponUsedId = couponUsedId;
                _couponService.UpdateCoupon(coupon);
            }
            else
            {
                couponUsedId = coupon.CouponUsedId.Value;
            }

            if (order.OrderItems.Count > 0)
            {
                foreach (var orderItem in order.OrderItems ?? new List<OrderItem>())
                {
                    orderItem.CouponUsedId = couponUsedId;
                }
            }
            order.CouponUsedId = couponUsedId;
            _orderRepository.Update(order);
        }

        public OrderVm InitOrder()
        {
            var userId = _httpContextAccessor.GetUserId();
            return new OrderVm
            {
                Customers = _customerService.GetCustomersInformationByUserId(userId).ToList(),
                Order = new OrderDto
                {
                    Ordered = DateTime.Now,
                    UserId = userId,
                    OrderItems = _orderItemService.GetOrderItemsForRealization(userId).ToList()
                },
                NewCustomer = new CustomerDetailsDto
                {
                    Addresses = new List<AddressDto> { new AddressDto() },
                    ContactDetails = new List<ContactDetailDto> { new ContactDetailDto() }
                }
            };
        }

        public int FulfillOrder(OrderVm model)
        {
            var addOrderDto = new AddOrderDto
            {
                Id = model.Order.Id,
                CustomerId = model.Order.CustomerId,
                PromoCode = model.PromoCode,
                OrderItems = model.Order.OrderItems?.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList()
                    ?? new List<OrderItemsIdsDto>()
            };

            if (model.CustomerData)
            {
                return AddOrder(addOrderDto);
            }

            var customerId = _customerService.AddCustomerDetails(model.NewCustomer);
            addOrderDto.CustomerId = customerId;
            return AddOrder(addOrderDto);
        }

        public NewOrderVm GetOrderSummaryById(int orderId)
        {
            var order = _orderRepository.GetOrderSummaryById(orderId);
            var orderVm = _mapper.Map<NewOrderVm>(order);
            return orderVm;
        }

        public OrderDetailsVm UpdateOrder(UpdateOrderDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(UpdateOrderDto).Name} cannot be null");
            }

            var order = _orderRepository.GetOrderDetailsById(dto.Id)
                ?? throw new BusinessException($"Order with id '{dto.Id}' was not found");
            var coupon = (CouponVm)null;
            if (!string.IsNullOrWhiteSpace(dto.PromoCode))
            {
                coupon = _couponService.GetCouponByCode(dto.PromoCode)
                    ?? throw new BusinessException($"Coupon code '{dto.PromoCode}' was not found");
            }
            var customer = _customerService.GetCustomer(dto.CustomerId)
                ?? throw new BusinessException($"Customer with id '{dto.CustomerId}' was not found");
            if (dto.CouponUsedId.HasValue && dto.CouponUsedId.Value != order.CouponUsedId)
            {
                throw new BusinessException($"Cannot assign existed coupon with id '{dto.CouponUsedId}'");
            }

            if (!string.IsNullOrWhiteSpace(dto.OrderNumber))
            {
                order.Number = dto.OrderNumber;
            }

            if (dto.Ordered.HasValue)
            {
                order.Ordered = dto.Ordered.Value;
            }

            order.CustomerId = dto.CustomerId;

            if (!order.IsDelivered && dto.IsDelivered)
            {
                order.IsDelivered = true;
                order.Delivered = DateTime.Now;
            }

            if (order.IsDelivered && !dto.IsDelivered)
            {
                order.IsDelivered = false;
                order.Delivered = null;
            }

            var orderItemsToAdd = dto.OrderItems?.Where(oi => oi.Id == 0 && oi.ItemId > 0 && oi.ItemOrderQuantity > 0) ?? Enumerable.Empty<AddOrderItemDto>();
            var orderItemsToModify = dto.OrderItems?.Where(oi => oi.Id > 0) ?? Enumerable.Empty<AddOrderItemDto>();
            var errors = new StringBuilder();
            foreach (var orderItemToModify in orderItemsToModify)
            {
                var orderItemExists = order.OrderItems?.FirstOrDefault(oi => oi.Id == orderItemToModify.Id);
                if (orderItemExists is null)
                {
                    errors.Append($"Order doesn't have order item with id '{orderItemToModify.Id}'.");
                }
            }
            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }

            var orderItemsToRemove = new List<OrderItem>();
            var currentOrderItems = new List<OrderItem>(order.OrderItems ?? Enumerable.Empty<OrderItem>());
            foreach (var orderItem in currentOrderItems)
            {
                var orderItemExists = orderItemsToModify.FirstOrDefault(oi => oi.Id == orderItem.Id);
                if (orderItemExists is null)
                {
                    order.OrderItems.Remove(orderItem);
                    orderItemsToRemove.Add(orderItem);
                    continue;
                }

                if (orderItem.ItemOrderQuantity != orderItemExists.ItemOrderQuantity)
                {
                    orderItem.ItemOrderQuantity = orderItemExists.ItemOrderQuantity;
                }
            }

            if (orderItemsToAdd.Any())
            {
                var items = _itemRepository.GetAll().Where(i => orderItemsToAdd.Select(it => it.ItemId).Contains(i.Id)).ToList();
                foreach(var item in orderItemsToAdd)
                {
                    var itemExists = items.FirstOrDefault(i => i.Id == item.ItemId);
                    if (itemExists is null)
                    {
                        continue;
                    }
                    var orderItem = new OrderItem
                    {
                        Id = 0,
                        ItemId = item.ItemId,
                        ItemOrderQuantity = item.ItemOrderQuantity,
                        OrderId = order.Id,
                        Item = itemExists
                    };
                    _orderItemRepository.AddOrderItem(orderItem);
                    order.OrderItems.Add(orderItem);
                }
            }

            _couponHandler.HandleCouponChangesOnUpdateOrder(coupon, order, new HandleCouponChangesDto(dto));
            CalculateCost(order);
            _paymentHandler.HandlePaymentChangesOnOrder(dto.Payment, order);
            _orderRepository.Update(order);
            orderItemsToRemove.ForEach(oi => _orderItemService.DeleteOrderItem(oi.Id));
            return _mapper.Map<OrderDetailsVm>(order);
        }

        private record OrderItemValidationModel(int Id, string UserId);
    }
}
