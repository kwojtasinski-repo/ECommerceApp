using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace ECommerceApp.Tests.Services.Order
{
    public class OrderServiceTests : BaseTest
    {
        private readonly Mock<IOrderRepository> _orderRepository = new Mock<IOrderRepository>();
        private readonly Mock<IOrderItemService> _orderItemService;
        private readonly Mock<IItemService> _itemService;
        private readonly Mock<ICouponService> _couponService;
        private readonly Mock<ICouponUsedRepository> _couponUsedRepository;
        private readonly Mock<ICustomerService> _customerService;
        private readonly HttpContextAccessorTest _httpContextAccessor;

        public OrderServiceTests()
        {
            _orderRepository = new Mock<IOrderRepository>();
            _orderItemService = new Mock<IOrderItemService>();
            _itemService = new Mock<IItemService>();
            _couponService = new Mock<ICouponService>();
            _couponUsedRepository = new Mock<ICouponUsedRepository>();
            _customerService = new Mock<ICustomerService>();
            _httpContextAccessor = new HttpContextAccessorTest();
        }

        [Fact]
        public void given_invalid_order_when_dispatching_order_should_throw_an_exception()
        {
            var orderId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException($"Order with id {orderId} not found, check your order if is not delivered and is paid");

            Action action = () => { orderService.DispatchOrder(orderId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_proper_order_when_dispatching_order_should_throw_an_exception()
        {
            var order = CreateDefaultOrder();
            order.IsPaid = true;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>() { order }.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.DispatchOrder(order.Id);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_proper_arguments_when_getting_all_orders_paid_should_return_orders()
        {
            var ordersCount = 3;
            var pageSize = 3;
            var pageNo = 1;
            var orders = CreateDefaultOrders(ordersCount, o => new Domain.Model.Order { Id = o.Id, Number = o.Number, Ordered = o.Ordered, Cost = o.Cost, CouponUsedId = o.CouponUsedId, CurrencyId = o.CurrencyId, CustomerId = o.CustomerId, Delivered = o.Delivered, IsDelivered = false, IsPaid = true, PaymentId = o.PaymentId, RefundId = o.RefundId, UserId = o.UserId, OrderItems = o.OrderItems, Currency = o.Currency, Customer = o.Customer, User = o.User });
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            var listOrders = orderService.GetAllOrdersPaid(pageSize, pageNo, "");

            listOrders.Should().NotBeNull();
            listOrders.SearchString.Should().BeEmpty();
            listOrders.PageSize.Should().Be(pageSize);
            listOrders.CurrentPage.Should().Be(pageNo);
            listOrders.Orders.Should().NotBeNull();
            listOrders.Orders.Count.Should().Be(orders.Count);
            listOrders.Count.Should().Be(orders.Count);
        }

        [Fact]
        public void given_invalid_page_size_when_getting_all_orders_paid_should_throw_an_exception()
        {
            var pageSize = 0;
            var pageNo = 1;   
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Page size should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersPaid(pageSize, pageNo, ""); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_page_no_when_getting_all_orders_paid_should_throw_an_exception()
        {
            var pageSize = 3;
            var pageNo = 0;
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Page number should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersPaid(pageSize, pageNo, ""); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_special_search_string_when_getting_all_orders_paid_should_return_orders()
        {
            var ordersCount = 3;
            var pageSize = 3;
            var pageNo = 1;
            var searchString = "253";
            var orders = CreateDefaultOrders(ordersCount, o => new Domain.Model.Order { Id = o.Id, Number = o.Number, Ordered = o.Ordered, Cost = o.Cost, CouponUsedId = o.CouponUsedId, CurrencyId = o.CurrencyId, CustomerId = o.CustomerId, Delivered = o.Delivered, IsDelivered = false, IsPaid = true, PaymentId = o.PaymentId, RefundId = o.RefundId, UserId = o.UserId, OrderItems = o.OrderItems, Currency = o.Currency, Customer = o.Customer, User = o.User });
            orders.First().Number = "25362654";
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            var listOrders = orderService.GetAllOrdersPaid(pageSize, pageNo, searchString);

            listOrders.Should().NotBeNull();
            listOrders.SearchString.Should().Be(searchString);
            listOrders.PageSize.Should().Be(pageSize);
            listOrders.CurrentPage.Should().Be(pageNo);
            listOrders.Orders.Should().NotBeNull();
            listOrders.Orders.Count.Should().Be(1);
            listOrders.Count.Should().Be(1);
        }

        [Fact]
        public void given_valid_parameters_when_adding_refun_to_order_should_update_order()
        {
            int orderId = 1;
            int refundId = 1;
            var orders = CreateDefaultOrders();
            var order = orders.First();
            order.IsPaid = true;
            order.Delivered = DateTime.Now;
            order.IsDelivered = true;
            order.OrderItems = CreateDefaultOrderItems(order);
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.AddRefundToOrder(orderId, refundId);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_id_when_adding_refun_to_order_should_throw_an_exception()
        {
            int orderId = 1;
            int refundId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException($"Order with id {orderId} not exists");

            Action action = () => { orderService.AddRefundToOrder(orderId, refundId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_page_size_when_getting_all_orders_by_user_id_should_throw_an_exception()
        {
            var pageSize = -9;
            var pageNo = 1;
            var userId = Guid.NewGuid().ToString();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Page size should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersByUserId(userId, pageSize, pageNo); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_page_no_when_getting_all_orders_by_user_id_should_throw_an_exception()
        {
            var pageSize = 3;
            var pageNo = -60;
            var userId = Guid.NewGuid().ToString();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Page number should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersByUserId(userId, pageSize, pageNo); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_coupon_id_and_order_when_adding_coupon_to_order_should_update_order()
        {
            var order = CreateDefaultNewOrderVm();
            var couponId = 1;
            var coupon = CreateDefaultCouponVm(couponId);
            var expectedCost = (1 - (decimal)coupon.Discount / 100) * order.Cost;
            _couponUsedRepository.Setup(c => c.AddCouponUsed(It.IsAny<CouponUsed>())).Verifiable();
            _couponService.Setup(c => c.GetCoupon(couponId)).Returns(coupon);
            _couponService.Setup(c => c.UpdateCoupon(It.IsAny<CouponVm>())).Verifiable();
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.AddCouponToOrder(couponId, order);

            order.Cost.Should().Be(expectedCost);
            _couponService.Verify(c => c.UpdateCoupon(It.IsAny<CouponVm>()), Times.Once);
            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_id_when_adding_coupon_to_order_should_throw_an_exception()
        {
            var couponId = 1;
            var order = new NewOrderVm();
            var expectedException = new BusinessException($"Coupon with id {couponId} doesnt exists");
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => { orderService.AddCouponToOrder(couponId, order); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_coupon_id_and_order_id_when_adding_coupon_used_to_order_should_update_order()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            var orderId = order.Id;
            var cost = order.Cost;
            var couponUsedId = 1;
            var coupon = CreateDefaultCouponVm(null);
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            Domain.Model.Order updatingOrder = null;
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Callback<Domain.Model.Order>(r => updatingOrder = r);
            _couponService.Setup(cu => cu.GetCouponFirstOrDefault(It.IsAny<Expression<Func<Domain.Model.Coupon, bool>>>())).Returns(coupon);
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.AddCouponUsedToOrder(orderId, couponUsedId);

            updatingOrder.Cost.Should().BeLessThan(cost);
            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_valid_coupon_id_and_invalid_order_id_when_adding_coupon_used_to_order_should_throw_an_exception()
        {
            var orderId = 1;
            var couponUsedId = 1;
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Cannot add coupon if order not exists");

            Action action = () => { orderService.AddCouponUsedToOrder(orderId, couponUsedId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_coupon_id_and_order_id_for_paid_order_when_adding_coupon_used_to_order_should_throw_an_exception()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            var orderId = order.Id;
            order.IsPaid = true;
            var cost = order.Cost;
            var couponUsedId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var coupon = CreateDefaultCouponVm(null);
            coupon.CouponUsedId = couponUsedId;
            _couponService.Setup(cu => cu.GetCouponFirstOrDefault(It.IsAny<Expression<Func<Domain.Model.Coupon, bool>>>())).Returns(coupon);
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Cannot add coupon to paid order");

            Action action = () => { orderService.AddCouponUsedToOrder(orderId, couponUsedId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);

        }

        [Fact]
        public void given_invalid_coupon_id_and_order_id_when_adding_coupon_used_to_order_should_update_order()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            var orderId = order.Id;
            var cost = order.Cost;
            var couponUsedId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Given invalid couponUsedId");

            Action action = () => { orderService.AddCouponUsedToOrder(orderId, couponUsedId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);

        }

        [Fact]
        public void given_valid_order_should_update()
        {
            var orderVm = CreateDefaultOrderDto();
            var cost = orderVm.Cost;
            var orders = CreateDefaultOrders();
            var order = orders.First();
            order.Id = orderVm.Id;
            var orderItems = CreateDefaultOrderItems(orderVm.OrderItems);
            orderItems.ForEach(oi => order.OrderItems.Add(oi));
            _orderRepository.Setup(o => o.GetAllOrders()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.UpdatedOrder(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.UpdateOrder(orderVm);

            _orderRepository.Verify(o => o.UpdatedOrder(It.IsAny<Domain.Model.Order>()), Times.Once);
            orderVm.Cost.Should().BeGreaterThan(cost);
        }

        [Fact]
        public void given_invalid_order_should_throw_an_exception() 
        {
            var orderVm = new OrderDto();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);
            var expectedException = new BusinessException("Items shouldnt be empty");

            Action action = () => { orderService.UpdateOrder(orderVm); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_order_with_new_order_items_should_update()
        {
            var orderVm = CreateDefaultOrderDto();
            orderVm.OrderItems.ForEach(oi => oi.Id = 0);
            var cost = orderVm.Cost;
            var orders = CreateDefaultOrders();
            var order = orders.First();
            order.Id = orderVm.Id;
            var orderItems = CreateDefaultOrderItems(orderVm.OrderItems);
            var items = orderItems.Select(oi => _mapper.Map<NewItemVm>(oi.Item));
            _orderRepository.Setup(o => o.GetAllOrders()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.GetAllOrderItems()).Returns(orderItems.AsQueryable());
            _orderRepository.Setup(o => o.UpdatedOrder(It.IsAny<Domain.Model.Order>())).Verifiable();
            _itemService.Setup(o => o.GetItems()).Returns(items.AsQueryable());
            _orderItemService.Setup(o => o.AddOrderItem(It.IsAny<OrderItemDto>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.UpdateOrder(orderVm);

            _orderRepository.Verify(o => o.UpdatedOrder(It.IsAny<Domain.Model.Order>()), Times.Once);
            orderVm.Cost.Should().BeGreaterThan(cost);
            _orderItemService.Verify(oi => oi.AddOrderItem(It.IsAny<OrderItemDto>()), Times.Exactly(orderItems.Count));
        }

        [Fact]
        public void given_invalid_order_with_one_item_not_ordered_by_user_should_update()
        {
            var orderVm = CreateDefaultOrderDto();
            orderVm.OrderItems.ForEach(oi => oi.Id = 0);
            var cost = orderVm.Cost;
            var orders = CreateDefaultOrders();
            var order = orders.First();
            order.Id = orderVm.Id;
            orderVm.OrderItems.ForEach(oi => order.OrderItems.Add(_mapper.Map<OrderItem>(oi)));
            var orderItem = orders.First().OrderItems.First();
            orderItem.UserId = "ABC";
            _orderRepository.Setup(o => o.GetAllOrders()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => { orderService.UpdateOrder(orderVm); };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_valid_order_should_delete_coupon_from_order()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            int orderId = order.Id;
            order.IsPaid = false;
            order.OrderItems = CreateDefaultOrderItems(order);
            var cost = order.Cost;
            var coupon = CreateDefaultCoupon();
            int couponUsedId = coupon.CouponUsedId.Value;
            order.CouponUsedId = couponUsedId;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _couponService.Setup(c => c.GetCouponFirstOrDefault(It.IsAny<Expression<Func<Domain.Model.Coupon, bool>>>())).Returns(_mapper.Map<CouponVm>(coupon));
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId);

            order.Cost.Should().BeGreaterThan(cost);
        }

        [Fact]
        public void given_invalid_order_when_deleting_coupon_from_order_should_throw_an_exception()
        {
            int orderId = 1;
            int couponUsedId = 1;
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => { orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId); };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_invalid_coupon_when_deleting_coupon_from_order_should_throw_an_exception()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            int orderId = order.Id;
            order.IsPaid = false;
            order.OrderItems = CreateDefaultOrderItems(order);
            var couponUsedId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => { orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId); };

            var exception = Assert.Throws<BusinessException>(action);
        }


        [Fact]
        public void given_paid_order_when_deleting_coupon_from_order_should_throw_an_exception()
        {
            var orders = CreateDefaultOrders();
            var order = orders.First();
            int orderId = order.Id;
            order.IsPaid = true;
            order.OrderItems = CreateDefaultOrderItems(order);
            var couponUsedId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => { orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId); };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_valid_order_should_add_order()
        {
            var order = CreateDefaultOrderDto();
            order.Id = 0;
            _orderRepository.Setup(o => o.AddOrder(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            orderService.AddOrder(new AddOrderDto { CustomerId = order.CustomerId, OrderItems = order.OrderItems.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList() });

            _orderRepository.Verify(o => o.AddOrder(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_should_add_order()
        {
            var order = CreateDefaultOrderDto();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () =>
            {
                orderService.AddOrder(new AddOrderDto { Id = order.Id, CustomerId = order.CustomerId, OrderItems = order.OrderItems.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList() });
            };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_null_order_when_add_should_throw_an_exception()
        {
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => orderService.AddOrder(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_order_when_update_should_throw_an_exception()
        {
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor);

            Action action = () => orderService.UpdateOrder((OrderDto) null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        #region DataInitial

        private Domain.Model.Order CreateDefaultOrder()
        {
            var order = new Domain.Model.Order();
            order.Id = new Random().Next(1, 9999);
            order.Number = "1234557567";
            order.Cost = new decimal(100);
            order.Ordered = DateTime.Now;
            order.IsPaid = false;
            order.IsDelivered = false;
            order.Delivered = null;
            order.CustomerId = 1;
            order.CurrencyId = 1;
            order.UserId = Guid.NewGuid().ToString();
            order.OrderItems = new List<Domain.Model.OrderItem>();
            order.Currency = new Domain.Model.Currency() { Id = 1 };
            order.Customer = new Domain.Model.Customer() { Id = 1 };
            order.User = new ApplicationUser { Id = order.UserId };
            return order;
        }

        private List<Domain.Model.Order> CreateDefaultOrders(int ordersCount = 3, Func<Domain.Model.Order, Domain.Model.Order> selector = null)
        {
            var orders = new List<Domain.Model.Order>();
            
            for (int i = 1; i <= ordersCount; i++)
            {
                var order = CreateDefaultOrder();
                order.Id = i;
                orders.Add(order);
            }

            if (selector != null)
            {
                orders = orders.Select(selector).ToList();
            }

            return orders;
        }

        private List<OrderItem> CreateDefaultOrderItems(Domain.Model.Order order, int orderItemCount = 3)
        {
            var orderItems = new List<OrderItem>();

            for (int i = 1; i <= orderItemCount; i++)
            {
                var orderItem = CreateDefaultOrderItem(order);
                orderItem.Id = i;
                orderItems.Add(orderItem);
            }

            return orderItems;
        }

        private OrderItem CreateDefaultOrderItem(Domain.Model.Order order)
        {
            var orderItem = new OrderItem();
            orderItem.Id = 1;
            orderItem.UserId = order.UserId;
            orderItem.User = order.User;
            orderItem.OrderId = order.Id;
            orderItem.Order = order;
            var itemId = new Random().Next(1, 9999);
            orderItem.ItemId = itemId;
            orderItem.Item = new Domain.Model.Item { Id = itemId, Cost = decimal.One };
            orderItem.ItemOrderQuantity = 1;
            return orderItem;
        }

        private NewOrderVm CreateDefaultNewOrderVm()
        {
            var order = new NewOrderVm()
            {
                Id = 1,
                Cost = Decimal.One,
                IsPaid = false,
                IsDelivered = false,
                CurrencyId = 1,
                CurrencyName = "PLN",
                Ordered = DateTime.Now,
                Number = Guid.NewGuid().ToString(),
                UserId = Guid.NewGuid().ToString(),
                CustomerId = new Random().Next(1, 200)
            };
            return order;
        }

        private CouponVm CreateDefaultCouponVm(int? couponIdIn)
        {
            var type = CreateDefaultCouponType();
            var couponId = new Random().Next(1, 9999);

            if (couponIdIn.HasValue)
            {
                couponId = couponIdIn.Value;
            }

            var coupon = new CouponVm()
            {
                Id = couponId,
                Code = "AB" + new Random().Next(1, 9999),
                CouponUsedId = 1,
                Description = "",
                Discount = new Random().Next(1, 20),
                CouponTypeId = type.Id
            };
            return coupon;
        }

        private CouponType CreateDefaultCouponType()
        {
            var couponType = new CouponType()
            {
                Id = new Random().Next(1, 9999),
                Type = "Coupon"
            };
            return couponType;
        }

        private Domain.Model.Coupon CreateDefaultCoupon()
        {
            var type = CreateDefaultCouponType();

            var coupon = new Domain.Model.Coupon()
            {
                Id = new Random().Next(1, 9999),
                Code = "ABVC",
                CouponUsedId = 1,
                CouponUsed = new CouponUsed() { Id = 1 },
                Discount = new Random().Next(1, 20),
                Type = type,
                CouponTypeId = type.Id
            };

            return coupon;
        }

        private static OrderDto CreateDefaultOrderDto()
        {
            OrderDto orderVm = new ()
            { 
                Id = new Random().Next(1, 9999),
                Cost = Decimal.One,
                IsPaid = false,
                IsDelivered = false,
                CurrencyId = 1,
                Ordered = DateTime.Now,
                Number = Guid.NewGuid().ToString(),
                UserId = Guid.NewGuid().ToString(),
                CustomerId = new Random().Next(1, 200)
            };
            orderVm.OrderItems = CreateDefaultOrderItemVm(orderVm);
            return orderVm;
        }

        private static List<OrderItemDto> CreateDefaultOrderItemVm(OrderDto orderDto, int count = 3)
        {
            var orderItems = new List<OrderItemDto>();
            
            for (int i = 1; i <= count; i++)
            {
                var orderItem = new OrderItemDto()
                {
                    Id = new Random().Next(1, 999),
                    ItemId = new Random().Next(1, 999),
                    ItemOrderQuantity = new Random().Next(1, 10),
                    UserId = orderDto.UserId,
                    OrderId = orderDto.Id
                };
                orderItems.Add(orderItem);
            }

            return orderItems;
        }

        private List<OrderItem> CreateDefaultOrderItems(List<OrderItemDto> orderItemsIn)
        {
            var orderItems = new List<OrderItem>();

            foreach(var orderItem in orderItemsIn)
            {
                var orderItemNew = new OrderItem()
                {
                    Id = orderItem.Id,
                    ItemId = orderItem.ItemId,
                    Item = new Domain.Model.Item() { Id = orderItem.ItemId, Cost = decimal.One },
                    UserId = orderItem.UserId,
                    User = new ApplicationUser { Id = orderItem.UserId },
                    OrderId = orderItem.OrderId,
                    ItemOrderQuantity = orderItem.ItemOrderQuantity
                };
                orderItems.Add(orderItemNew);
            }

            return orderItems;
        }
        
        #endregion
    }
}
