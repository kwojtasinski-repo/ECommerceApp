using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.Order
{ 
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepository = new Mock<IOrderRepository>();
        private readonly IMapper _mapper;
        private readonly Mock<IOrderItemService> _orderItemService;
        private readonly Mock<IItemService> _itemService;
        private readonly Mock<ICouponService> _couponService;
        private readonly Mock<ICouponUsedRepository> _couponUsedRepository;
        private readonly Mock<ICustomerService> _customerService;

        public OrderServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _orderRepository = new Mock<IOrderRepository>();
            _orderItemService = new Mock<IOrderItemService>();
            _itemService = new Mock<IItemService>();
            _couponService = new Mock<ICouponService>();
            _couponUsedRepository = new Mock<ICouponUsedRepository>();
            _customerService = new Mock<ICustomerService>();
        }

        [Fact]
        public void given_invalid_order_when_dispatching_order_should_throw_an_exception()
        {
            var orderId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
            var expectedException = new BusinessException($"Order with id {orderId} not found, check your order if is not delivered and is paid");

            Action action = () => { orderService.DispatchOrder(orderId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_proper_order_when_dispatching_order_should_throw_an_exception()
        {
            var order = GetDefaultOrder();
            order.IsPaid = true;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>() { order }.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

            orderService.DispatchOrder(order.Id);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_proper_arguments_when_getting_all_orders_paid_should_return_orders()
        {
            var ordersCount = 3;
            var pageSize = 3;
            var pageNo = 1;
            var orders = GetDefaultOrders(ordersCount, o => new Domain.Model.Order { Id = o.Id, Number = o.Number, Ordered = o.Ordered, Cost = o.Cost, CouponUsedId = o.CouponUsedId, CurrencyId = o.CurrencyId, CustomerId = o.CustomerId, Delivered = o.Delivered, IsDelivered = false, IsPaid = true, PaymentId = o.PaymentId, RefundId = o.RefundId, UserId = o.UserId, OrderItems = o.OrderItems, Currency = o.Currency, Customer = o.Customer, User = o.User });
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

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
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
            var expectedException = new BusinessException("Page size should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersPaid(pageSize, pageNo, ""); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_page_no_when_getting_all_orders_paid_should_throw_an_exception()
        {
            var pageSize = 3;
            var pageNo = 0;
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
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
            var orders = GetDefaultOrders(ordersCount, o => new Domain.Model.Order { Id = o.Id, Number = o.Number, Ordered = o.Ordered, Cost = o.Cost, CouponUsedId = o.CouponUsedId, CurrencyId = o.CurrencyId, CustomerId = o.CustomerId, Delivered = o.Delivered, IsDelivered = false, IsPaid = true, PaymentId = o.PaymentId, RefundId = o.RefundId, UserId = o.UserId, OrderItems = o.OrderItems, Currency = o.Currency, Customer = o.Customer, User = o.User });
            orders.First().Number = 25362654;
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

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
            var orders = GetDefaultOrders();
            var order = orders.First();
            order.IsPaid = true;
            order.Delivered = DateTime.Now;
            order.IsDelivered = true;
            order.OrderItems = GetDefaultOrderItems(order);
            _orderRepository.Setup(o => o.GetAll()).Returns(orders.AsQueryable());
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

            orderService.AddRefundToOrder(orderId, refundId);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_id_when_adding_refun_to_order_should_throw_an_exception()
        {
            int orderId = 1;
            int refundId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
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
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
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
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);
            var expectedException = new BusinessException("Page number should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersByUserId(userId, pageSize, pageNo); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_parameters_when_adding_coupon_to_order_should_update_order()
        {
            var order = CreateDefaultNewOrderVm();
            var couponId = 1;
            var coupon = CreateDefaultCouponVm(couponId);
            var expectedCost = (1 - (decimal)coupon.Discount / 100) * order.Cost;
            _couponUsedRepository.Setup(c => c.AddCouponUsed(It.IsAny<CouponUsed>())).Verifiable();
            _couponService.Setup(c => c.GetCoupon(couponId)).Returns(coupon);
            _couponService.Setup(c => c.UpdateCoupon(It.IsAny<CouponVm>())).Verifiable();
            _orderRepository.Setup(o => o.Update(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

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
            var orderService = new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object);

            Action action = () => { orderService.AddCouponToOrder(couponId, order); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        #region DataInitial

        private Domain.Model.Order GetDefaultOrder()
        {
            var order = new Domain.Model.Order();
            order.Id = new Random().Next(1, 9999);
            order.Number = 12345;
            order.Cost = new decimal(100);
            order.Ordered = DateTime.Now;
            order.IsPaid = false;
            order.IsDelivered = false;
            order.Delivered = null;
            order.CustomerId = 1;
            order.CurrencyId = 1;
            order.UserId = Guid.NewGuid().ToString();
            order.OrderItems = new List<OrderItem>();
            order.Currency = new Currency() { Id = 1 };
            order.Customer = new Customer() { Id = 1 };
            order.User = new Microsoft.AspNetCore.Identity.IdentityUser { Id = order.UserId };
            return order;
        }

        private List<Domain.Model.Order> GetDefaultOrders(int ordersCount = 3, Func<Domain.Model.Order, Domain.Model.Order> selector = null)
        {
            var orders = new List<Domain.Model.Order>();
            
            for (int i = 1; i <= ordersCount; i++)
            {
                var order = GetDefaultOrder();
                order.Id = i;
                orders.Add(order);
            }

            if (selector != null)
            {
                orders = orders.Select(selector).ToList();
            }

            return orders;
        }

        private List<OrderItem> GetDefaultOrderItems(Domain.Model.Order order, int orderItemCount = 3)
        {
            var orderItems = new List<OrderItem>();

            for (int i = 1; i <= orderItemCount; i++)
            {
                var orderItem = GetDefaultOrderItem(order);
                orderItem.Id = i;
                orderItems.Add(orderItem);
            }

            return orderItems;
        }

        private OrderItem GetDefaultOrderItem(Domain.Model.Order order)
        {
            var orderItem = new OrderItem();
            orderItem.Id = 1;
            orderItem.UserId = order.UserId;
            orderItem.User = order.User;
            orderItem.OrderId = order.Id;
            orderItem.Order = order;
            var itemId = new Random().Next(1, 9999);
            orderItem.ItemId = itemId;
            orderItem.Item = new Item { Id = itemId };
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
                Number = new Random().Next(1, 200),
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

        #endregion
    }
}
