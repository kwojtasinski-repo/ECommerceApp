using AutoMapper;
using ECommerceApp.Application.Constants;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IUserContext _userContext;
        private readonly IPaymentHandler _paymentHandler;
        private readonly ICouponHandler _couponHandler;
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IItemRepository _itemRepository;

        public OrderService(IOrderRepository orderRepo, IMapper mapper, IOrderItemService orderItemService, IItemService itemService, ICouponService couponService, ICouponUsedRepository couponUsedRepository, ICustomerService customerService,
                IUserContext userContext, IPaymentHandler paymentHandler, ICouponHandler couponHandler,
                IOrderItemRepository orderItemRepository, IItemRepository itemRepository)
        {
            _orderRepository = orderRepo;
            _mapper = mapper;
            _orderItemService = orderItemService;
            _itemService = itemService;
            _couponService = couponService;
            _couponUsedRepository = couponUsedRepository;
            _customerService = customerService;
            _userContext = userContext;
            _paymentHandler = paymentHandler;
            _couponHandler = couponHandler;
            _orderItemRepository = orderItemRepository;
            _itemRepository = itemRepository;
        }

        public void Update(NewOrderVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(NewOrderVm).Name} cannot be null");
            }

            var order = _mapper.Map<Order>(vm);
            _orderRepository.UpdatedOrder(order);
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

        public void DeleteRefundFromOrder(int refundId)
        {
            var order = _orderRepository.GetOrderByRefundId(refundId)
                   ?? throw new BusinessException($"Refund with id '{refundId}' was not assigned to any order", "refundNotAssignedToAnyOrder", new Dictionary<string, string> { { "id", $"{refundId}" } });

            order.RefundId = null;
            foreach (var oi in order.OrderItems)
            {
                oi.RefundId = null;
            }

            _orderRepository.UpdatedOrder(order);
        }

        public void DeleteCouponUsedFromOrder(int orderId, int couponUsedId)
        {
            var order = _orderRepository.GetOrderById(orderId)
                ?? throw new BusinessException($"Order with id '{orderId}' was not found", "orderNotFound", new Dictionary<string, string> { { "id", $"{orderId}"} });
            if (order.IsPaid)
            {
                throw new BusinessException("Cannot delete coupon when order is paid", "cannotDeleteCouponFromPaidOrder");
            }

            var coupon = _couponService.GetByCouponUsed(couponUsedId)
                ?? throw new BusinessException($"Coupon with id '{couponUsedId}' was not found", "couponNotFound", new Dictionary<string, string> { { "id", $"{couponUsedId}" } });
            if (coupon.CouponUsedId != order.CouponUsedId)
            {
                throw new BusinessException($"Coupon with id '{couponUsedId}' connected with order with id '{order.Id}' was not found", "couponConnectWithOrderNotFound", new Dictionary<string, string> { { "id", $"{couponUsedId}" }, { "orderId", $"{order.Id}" } });
            }

            order.CouponUsedId = null;
            foreach (var orderItem in order.OrderItems)
            {
                orderItem.CouponUsedId = null;
            }

            order.Cost /= (1 - (decimal)coupon.Discount / 100);
            _orderRepository.UpdatedOrder(order);
        }

        public ListForOrderVm GetAllOrders(int pageSize, int pageNo, string searchString)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders(pageSize, pageNo, searchString));

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Orders = orders,
                Count = _orderRepository.GetCountBySearchString(searchString)
            };

            return ordersList;
        }

        public List<OrderForListVm> GetAllOrders()
        {
            return _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders());
        }

        public OrderDetailsVm GetOrderDetail(int id)
        {
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role) 
                && !_orderRepository.ExistsByIdAndUserId(id, _userContext.UserId))
            {
                return null;
            }
            var orderDetails = _orderRepository.GetOrderDetailsById(id);
            var orderDetailsVm = _mapper.Map<OrderDetailsVm>(orderDetails);
            return orderDetailsVm;
        }

        public void AddCouponUsedToOrder(int orderId, int couponUsedId)
        {
            var order = _orderRepository.GetOrderById(orderId)
                ?? throw new BusinessException("Cannot add coupon if order not exists", "orderNotExistsWhileAddCoupon");
            
            var coupon = _couponService.GetByCouponUsed(couponUsedId)
                ?? throw new BusinessException($"Coupon used with id '{couponUsedId}' was not found", "couponUsedNotFound", new Dictionary<string, string> { { "id", $"{couponUsedId}"} });

            if (order.IsPaid)
            {
                throw new BusinessException("Cannot add coupon to paid order", "addCouponToPaidOrderNotAllowed");
            }

           order.Cost = (1 - (decimal)coupon.Discount / 100) * order.Cost;

            if (order.OrderItems.Count > 0)
            {
                foreach (var orderItem in order.OrderItems ?? new List<OrderItem>())
                {
                    orderItem.CouponUsedId = couponUsedId;
                }
            }
            order.CouponUsedId = couponUsedId;
            _orderRepository.UpdatedOrder(order);
        }

        public int AddCouponToOrder(int couponId, NewOrderVm order)
        {
            if (order is null)
            {
                throw new BusinessException($"{typeof(NewOrderVm).Name} cannot be null");
            }

            var coupon = _couponService.GetCoupon(couponId) ?? throw new BusinessException($"Coupon with id {couponId} doesnt exists", "couponNotFound", new Dictionary<string, string> { { "id", $"{couponId}"} });
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
            return _mapper.Map<OrderDto>(_orderRepository.GetOrderById(orderId));
        }

        public NewOrderVm GetOrderForRealization(int orderId)
        {
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role)
                && !_orderRepository.ExistsByIdAndUserId(orderId, _userContext.UserId))
            {
                return null;
            }

            var order = _orderRepository.GetOrderForRealizationById(orderId);
            if (order is null)
            {
                return null;
            }

            var orderVm = _mapper.Map<NewOrderVm>(order);
            // TODO: in future fetch items from frontend
            orderVm.Items = _itemService.GetAllItems();
            orderVm.UserId = _userContext.UserId;
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
            orderVm.Items = _itemService.GetAllItems();
            orderVm.UserId = _userContext.UserId;
            return orderVm;
        }

        public ListForOrderVm GetAllOrdersByCustomerId(int customerId, int pageSize, int pageNo)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);
            if (_orderRepository.ExistsByCustomerIdAndUserId(customerId, _userContext.UserId))
            {
                return new ListForOrderVm { Orders = new List<OrderForListVm>() };
            }

            var orders = _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders(customerId, pageSize, pageNo));

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                Orders = orders,
                Count = _orderRepository.GetCountByCustomerId(customerId)
            };

            return ordersList;
        }

        public List<OrderForListVm> GetAllOrdersByCustomerId(int customerId)
        {
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role)
                && !_orderRepository.ExistsByCustomerIdAndUserId(customerId, _userContext.UserId))
            {
                return new List<OrderForListVm>();
            }
            return _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders(customerId));
        }

        public ListForOrderVm GetAllOrdersByUserId(string userId, int pageSize, int pageNo)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders(userId, pageSize, pageNo));

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = "",
                Orders = orders,
                Count = _orderRepository.GetCountByUserId(userId)
            };

            return ordersList;
        }

        public List<OrderForListVm> GetAllOrdersByUserId(string userId)
        {
            return _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllOrders(userId));
        }

        public void AddRefundToOrder(int orderId, int refundId)
        {
            var order = _orderRepository.GetOrderPaidAndDeliveredById(orderId)
                ?? throw new BusinessException($"Order with id {orderId} not exists", "orderNotFound", new Dictionary<string, string> { { "id", $"{orderId}"} });
            order.RefundId = refundId;
            foreach (var oi in order.OrderItems)
            {
                oi.RefundId = refundId;
            }
            _orderRepository.UpdatedOrder(order);
        }

        public ListForOrderVm GetAllOrdersPaid(int pageSize, int pageNo, string searchString)
        {
            ValidatePageSizeAndPageNo(pageSize, pageNo);

            var orders = _mapper.Map<List<OrderForListVm>>(_orderRepository.GetAllPaidOrders(pageSize, pageNo, searchString));

            var ordersList = new ListForOrderVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Orders = orders,
                Count = _orderRepository.GetCountPaidOrdersBySearchString(searchString)
            };

            return ordersList;
        }

        public void DispatchOrder(int orderId)
        {
            var order = _orderRepository.GetOrderPaidAndNotDelivered(orderId)
                ?? throw new BusinessException($"Order with id {orderId} not found, check your order if is not delivered and is paid", "orderNotFoundCheckIfPaidDelivered", new Dictionary<string, string> { { "id", $"{orderId}" } });
            order.IsDelivered = true;
            order.Delivered = DateTime.Now;
            _orderRepository.UpdatedOrder(order);
        }

        public int AddOrderFromCart(AddOrderFromCartDto model)
        {
            var userId = _userContext.UserId;
            var dto = new AddOrderDto
            {
                CustomerId = model.CustomerId,
                PromoCode = model.PromoCode,
                OrderItems = _orderItemService.GetOrderItemsIdsForRealization(_userContext.UserId)
                    .Select(id => new OrderItemsIdsDto { Id = id }).ToList()
            };
            var id = AddOrderInternal(dto);
            return id;
        }

        public OrderVm InitOrder()
        {
            var userId = _userContext.UserId;
            return new OrderVm
            {
                Customers = _customerService.GetCustomersInformationByUserId(userId),
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
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role)
                && !_orderRepository.ExistsByIdAndUserId(orderId, _userContext.UserId))
            {
                return null;
            }
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

            var order = _orderRepository.GetOrderDetailsById(dto.Id);
            if (order is null )
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dto.PromoCode) && !_couponService.ExistsByCode(dto.PromoCode))
            {
                throw new BusinessException($"Coupon code '{dto.PromoCode}' was not found", "couponWithCodeNotFound", new Dictionary<string, string> { { "code", dto.PromoCode } });
            }

            var customer = _customerService.GetCustomer(dto.CustomerId)
                ?? throw new BusinessException($"Customer with id '{dto.CustomerId}' was not found", "customerNotFound", new Dictionary<string, string> { { "id", $"{dto.CustomerId}"} });
            if (dto.CouponUsedId.HasValue && dto.CouponUsedId.Value != order.CouponUsedId)
            {
                throw new BusinessException($"Cannot assign existed coupon with id '{dto.CouponUsedId}'", "existedCouponAssignNotAllowed", new Dictionary<string, string> { { "id", $"{dto.CouponUsedId}" } });
            }

            UpdateOrderFields(order, dto);
            var orderItemsToRemove = HandleUpdateOrderItems(order, dto);
            order.CalculateCost();
            _couponHandler.HandleCouponChangesOnOrder(order, new HandleCouponChangesDto(dto));
            _paymentHandler.HandlePaymentChangesOnOrder(dto.Payment, order);
            _orderRepository.UpdatedOrder(order);
            orderItemsToRemove.ForEach(oi => _orderItemService.DeleteOrderItem(oi.Id));
            var dto2 = _mapper.Map<OrderDetailsVm>(order);
            return dto2;
        }

        private void UpdateOrderFields(Order order, UpdateOrderDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.OrderNumber))
                order.Number = dto.OrderNumber;

            if (dto.Ordered.HasValue)
                order.Ordered = dto.Ordered.Value;

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

        private int AddOrderInternal(AddOrderDto model)
        {
            var dto = model.AsDto();
            if (!_customerService.ExistsById(dto.CustomerId))
            {
                throw new BusinessException($"Customer with id '{dto.CustomerId}' was not found", "customerNotFound", new Dictionary<string, string> { { "id", $"{dto.CustomerId}" } });
            }

            dto.CurrencyId = CurrencyConstants.PlnId;
            dto.UserId = _userContext.UserId;
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
            ValidUserOrderItems(order, order.OrderItems);
            order.CalculateCost();
            var id = _orderRepository.AddOrder(order);
            _couponHandler.HandleCouponChangesOnOrder(order, HandleCouponChangesDto.Of(model.PromoCode));
            return id;
        }

        private static void ValidUserOrderItems(Order order, ICollection<OrderItem> itemsFromDb)
        {
            StringBuilder errors = new();

            foreach (var orderItem in order.OrderItems ?? new List<OrderItem>())
            {
                var item = itemsFromDb.Where(i => i.Id == orderItem.Id).FirstOrDefault();
                if (item == default)
                {
                    continue;
                }

                if (order.UserId != item.UserId)
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

        private List<OrderItem> HandleUpdateOrderItems(Order order, UpdateOrderDto dto)
        {
            var orderItemsToAdd = dto.OrderItems?.Where(oi => oi.Id == 0 && oi.ItemId > 0 && oi.ItemOrderQuantity > 0) ?? Enumerable.Empty<AddOrderItemDto>();
            var orderItemsToModify = dto.OrderItems?.Where(oi => oi.Id > 0) ?? Enumerable.Empty<AddOrderItemDto>();
            ValidateOrderItems(order, orderItemsToModify);
            var orderItemsToRemove = DeleteOrderItemsFromOrder(order, orderItemsToModify);
            UpdateOrderItemsOnOrder(order, orderItemsToModify);
            if (!orderItemsToAdd.Any())
            {
                return orderItemsToRemove;
            }

            AddOrderItemsToOrder(order, orderItemsToAdd);
            return orderItemsToRemove;
        }

        private static void ValidateOrderItems(Order order, IEnumerable<AddOrderItemDto> orderItems)
        {
            var errors = new StringBuilder();
            foreach (var orderItemToModify in orderItems)
            {
                var orderItemExists = order.OrderItems?.FirstOrDefault(oi => oi.Id == orderItemToModify.Id);
                if (orderItemExists is null)
                {
                    errors.Append($"Order doesn't have item with id '{orderItemToModify.Id}'.");
                }
            }
            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
        }

        private static List<OrderItem> DeleteOrderItemsFromOrder(Order order, IEnumerable<AddOrderItemDto> orderItemsToModify)
        {
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
            }
            return orderItemsToRemove;
        }

        private static void UpdateOrderItemsOnOrder(Order order, IEnumerable<AddOrderItemDto> orderItemsToModify)
        {
            foreach (var orderItem in order.OrderItems)
            {
                var orderItemExists = orderItemsToModify.FirstOrDefault(oi => oi.Id == orderItem.Id);
                if (orderItemExists is null)
                {
                    continue;
                }

                if (orderItem.ItemOrderQuantity != orderItemExists.ItemOrderQuantity)
                {
                    orderItem.ItemOrderQuantity = orderItemExists.ItemOrderQuantity;
                }
            }
        }

        private void AddOrderItemsToOrder(Order order, IEnumerable<AddOrderItemDto> orderItemsToAdd)
        {
            var items = _itemRepository.GetItemsByIds(orderItemsToAdd.Select(it => it.ItemId));
            foreach (var item in orderItemsToAdd)
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

        public int GetCustomerFromOrder(int orderId)
        {
            return _orderRepository.GetCustomerFromOrder(orderId);
        }
    }
}
