using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
        private readonly Mock<IPaymentRepository> _paymentRepository;
        private readonly Mock<ICurrencyRateService> _currencyRateService;
        private readonly Mock<ICouponRepository> _couponRepository;
        private readonly IPaymentHandler _paymentHandler;
        private readonly ICouponHandler _couponHandler;
        private readonly Mock<IOrderItemRepository> _orderItemRepository;
        private readonly Mock<IItemRepository> _itemRepository;

        public OrderServiceTests()
        {
            _orderRepository = new Mock<IOrderRepository>();
            _orderItemService = new Mock<IOrderItemService>();
            _itemService = new Mock<IItemService>();
            _couponService = new Mock<ICouponService>();
            _couponUsedRepository = new Mock<ICouponUsedRepository>();
            _customerService = new Mock<ICustomerService>();
            _httpContextAccessor = new HttpContextAccessorTest();
            _paymentRepository = new Mock<IPaymentRepository>();
            _currencyRateService = new Mock<ICurrencyRateService>();
            _couponRepository = new Mock<ICouponRepository>();
            _paymentHandler = new PaymentHandler(_paymentRepository.Object, _currencyRateService.Object);
            _couponHandler = new CouponHandler(_couponRepository.Object, _couponUsedRepository.Object);
            _orderItemRepository = new Mock<IOrderItemRepository>();
            _itemRepository = new Mock<IItemRepository>();
        }

        private OrderService CreateService()
        {
            return new OrderService(_orderRepository.Object, _mapper, _orderItemService.Object, _itemService.Object, _couponService.Object, _couponUsedRepository.Object, _customerService.Object, _httpContextAccessor,
                    _paymentHandler, _couponHandler, _orderItemRepository.Object, _itemRepository.Object
                );
        }

        [Fact]
        public void given_invalid_order_when_dispatching_order_should_throw_an_exception()
        {
            var orderId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = CreateService();
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
            var orderService = CreateService();

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
            var orderService = CreateService();

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
            var orderService = CreateService();
            var expectedException = new BusinessException("Page size should be positive and greater than 0");

            Action action = () => { orderService.GetAllOrdersPaid(pageSize, pageNo, ""); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_page_no_when_getting_all_orders_paid_should_throw_an_exception()
        {
            var pageSize = 3;
            var pageNo = 0;
            var orderService = CreateService();
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
            var orderService = CreateService();

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
            var orderService = CreateService();

            orderService.AddRefundToOrder(orderId, refundId);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_id_when_adding_refun_to_order_should_throw_an_exception()
        {
            int orderId = 1;
            int refundId = 1;
            _orderRepository.Setup(o => o.GetAll()).Returns(new List<Domain.Model.Order>().AsQueryable());
            var orderService = CreateService();
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
            var orderService = CreateService();
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
            var orderService = CreateService();
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
            var orderService = CreateService();

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
            var orderService = CreateService();

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
            var orderService = CreateService();

            orderService.AddCouponUsedToOrder(orderId, couponUsedId);

            updatingOrder.Cost.Should().BeLessThan(cost);
            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_valid_coupon_id_and_invalid_order_id_when_adding_coupon_used_to_order_should_throw_an_exception()
        {
            var orderId = 1;
            var couponUsedId = 1;
            var orderService = CreateService();
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
            var orderService = CreateService();
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
            var orderService = CreateService();
            var expectedException = new BusinessException("Given invalid couponUsedId");

            Action action = () => { orderService.AddCouponUsedToOrder(orderId, couponUsedId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);

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
            var orderService = CreateService();

            orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId);

            order.Cost.Should().BeGreaterThan(cost);
        }

        [Fact]
        public void given_invalid_order_when_deleting_coupon_from_order_should_throw_an_exception()
        {
            int orderId = 1;
            int couponUsedId = 1;
            var orderService = CreateService();

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
            var orderService = CreateService();

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
            var orderService = CreateService();

            Action action = () => { orderService.DeleteCouponUsedFromOrder(orderId, couponUsedId); };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_valid_order_should_add_order()
        {
            var order = CreateDefaultOrderDto();
            order.Id = 0;
            _orderRepository.Setup(o => o.AddOrder(It.IsAny<Domain.Model.Order>())).Verifiable();
            var orderService = CreateService();

            orderService.AddOrder(new AddOrderDto { CustomerId = order.CustomerId, OrderItems = order.OrderItems.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList() });

            _orderRepository.Verify(o => o.AddOrder(It.IsAny<Domain.Model.Order>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_should_add_order()
        {
            var order = CreateDefaultOrderDto();
            var orderService = CreateService();

            Action action = () =>
            {
                orderService.AddOrder(new AddOrderDto { Id = order.Id, CustomerId = order.CustomerId, OrderItems = order.OrderItems.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList() });
            };

            var exception = Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_null_order_when_add_should_throw_an_exception()
        {
            var orderService = CreateService();

            Action action = () => orderService.AddOrder(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_dto_when_update_order_should_throw_an_exception()
        {
            var orderService = CreateService();

            Action action = () => orderService.UpdateOrder((UpdateOrderDto) null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_not_existed_order_when_update_should_throw_exceptio_with_message_not_found()
        {
            var orderService = CreateService();
            var dto = new UpdateOrderDto { Id = 1 };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Order with id '{dto.Id}' was not found");
        }

        [Fact]
        public void given_not_existed_customer_when_update_should_throw_exceptio_with_message_not_found()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var dto = new UpdateOrderDto { Id = order.Id, CustomerId = order.CustomerId };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Order with id '{dto.Id}' was not found");
        }

        [Fact]
        public void given_not_existed_promo_code_when_update_should_throw_exceptio_with_message_not_found()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            AddOrder(order);
            var dto = new UpdateOrderDto { Id = order.Id, CustomerId = order.CustomerId, PromoCode = "Abc" };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Coupon code '{dto.PromoCode}' was not found");
        }

        [Fact]
        public void given_different_coupon_used_id_when_update_should_throw_exceptio_with_message_cannot_assign_existed_coupon()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            AddOrder(order);
            var dto = new UpdateOrderDto { Id = order.Id, CustomerId = order.CustomerId, CouponUsedId = 10 };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Cannot assign existed coupon with id '{dto.CouponUsedId}'");
        }

        [Fact]
        public void given_different_payment_id_when_update_should_throw_exception_with_message_cannot_assign_existed_payment()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            AddOrder(order);
            var dto = new UpdateOrderDto { Id = order.Id, CustomerId = order.CustomerId, Payment = new PaymentInfoDto { Id = 10 } };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Cannot assign existed payment with id '{dto.Payment.Id}'");
        }

        [Fact]
        public void given_valid_update_order_dto_should_update()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            AddOrder(order);
            AddCustomer(customer);
            var items = GenerateAndAddItems();
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[1].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[3].Id, ItemOrderQuantity = 10 },
                }
            };
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count);
        }

        [Fact]
        public void given_valid_update_order_dto_with_existing_items_and_new_should_update_order_and_return_more_order_items()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var items = GenerateAndAddItems();
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                }
            };
            orderItems.ForEach(oi => dto.OrderItems.Add(new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity }));
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            exepectedCost += orderItems.Select(oi => new
                                                {
                                                    Cost = (oi.Item?.Cost ?? 0) * oi.ItemOrderQuantity,
                                                })
                                                .Sum(i => i.Cost);
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
        }

        [Fact]
        public void given_valid_update_order_dto_with_existing_modified_items_and_new_should_update_order()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var orderItemsInOrder = 3;
            var items = GenerateAndAddItems();
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                }
            };
            var index = 0;
            orderItems.ForEach(oi => {
                if (index == orderItemsInOrder) { return; }
                dto.OrderItems.Add(new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity + 2 });
                index++;
            });
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            exepectedCost += dto.OrderItems.Select(oi => new
                                                {
                                                    Cost = (
                                                        (orderItems.Any(oiInner => oiInner.Id == oi.Id))
                                                        ? (orderItems.FirstOrDefault(oiInner => oiInner.Id == oi.Id).Item?.Cost ?? 0) * oi.ItemOrderQuantity
                                                        : 0)
                                                })
                                                .Sum(i => i.Cost);
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            _orderItemService.Verify(o => o.DeleteOrderItem(It.IsAny<int>()), times: Times.Exactly(2));
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count());
        }

        [Fact]
        public void given_valid_update_order_dto_with_existing_coupon_modified_items_and_new_should_update_order()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var orderItemsInOrder = 3;
            var items = GenerateAndAddItems();
            order.CouponUsed = CreateDefaultCouponUsed(order.Id);
            order.CouponUsedId = order.CouponUsed.Id;
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                },
                CouponUsedId = order.CouponUsedId,
            };
            var index = 0;
            orderItems.ForEach(oi => {
                if (index == orderItemsInOrder) { return; }
                dto.OrderItems.Add(new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity + 2 });
                index++;
            });
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            exepectedCost += dto.OrderItems.Select(oi => new
                                                {
                                                    Cost = (
                                                        (orderItems.Any(oiInner => oiInner.Id == oi.Id))
                                                        ? (orderItems.FirstOrDefault(oiInner => oiInner.Id == oi.Id).Item?.Cost ?? 0) * oi.ItemOrderQuantity
                                                        : 0)
                                                })
                                                .Sum(i => i.Cost);
            exepectedCost = (1 - (order.CouponUsed.Coupon.Discount/100M)) * exepectedCost;
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            _orderItemService.Verify(o => o.DeleteOrderItem(It.IsAny<int>()), times: Times.Exactly(2));
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count());
            dtoUpdated.CouponUsedId.Should().NotBeNull();
        }
        
        [Fact]
        public void given_valid_update_order_dto_without_existing_coupon_and_with_modified_items_and_new_should_update_order()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var orderItemsInOrder = 3;
            var items = GenerateAndAddItems();
            order.CouponUsed = CreateDefaultCouponUsed(order.Id);
            order.CouponUsedId = order.CouponUsed.Id;
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                }
            };
            var index = 0;
            orderItems.ForEach(oi => {
                if (index == orderItemsInOrder) { return; }
                dto.OrderItems.Add(new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity + 2 });
                index++;
            });
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            exepectedCost += dto.OrderItems.Select(oi => new
                                                {
                                                    Cost = (
                                                        (orderItems.Any(oiInner => oiInner.Id == oi.Id))
                                                        ? (orderItems.FirstOrDefault(oiInner => oiInner.Id == oi.Id).Item?.Cost ?? 0) * oi.ItemOrderQuantity
                                                        : 0)
                                                })
                                                .Sum(i => i.Cost);
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            _orderItemService.Verify(o => o.DeleteOrderItem(It.IsAny<int>()), times: Times.Exactly(2));
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count());
            dtoUpdated.CouponUsedId.Should().BeNull();
        }
        
        [Fact]
        public void given_valid_update_order_dto_with_new_items_and_revert_delivery_should_update_order()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            order.IsDelivered = true;
            order.Delivered = DateTime.Now;
            AddOrder(order);
            AddCustomer(customer);
            var items = GenerateAndAddItems();
            order.CouponUsed = CreateDefaultCouponUsed(order.Id);
            order.CouponUsedId = order.CouponUsed.Id;
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = false,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                }
            };
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Once);
            _orderItemService.Verify(o => o.DeleteOrderItem(It.IsAny<int>()), times: Times.Exactly(5));
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeFalse();
            dtoUpdated.Delivered.HasValue.Should().BeFalse();
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count());
            dtoUpdated.CouponUsedId.Should().BeNull();
        }

        [Fact]
        public void given_valid_update_order_dto_with_new_coupon_and_with_modified_items_and_new_should_update_order()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var coupon = CreateDefaultCoupon();
            coupon.CouponUsedId = null;
            coupon.CouponUsed = null;
            AddCoupon(coupon);
            _couponUsedRepository.Setup(c => c.Delete(It.IsAny<int>())).Returns(true);
            var orderItemsInOrder = 3;
            var items = GenerateAndAddItems();
            order.CouponUsed = CreateDefaultCouponUsed(order.Id);
            order.CouponUsedId = order.CouponUsed.Id;
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                },
                PromoCode = coupon.Code,
            };
            var index = 0;
            orderItems.ForEach(oi => {
                if (index == orderItemsInOrder) { return; }
                dto.OrderItems.Add(new AddOrderItemDto { Id = oi.Id, ItemId = oi.ItemId, ItemOrderQuantity = oi.ItemOrderQuantity + 2 });
                index++;
            });
            var exepectedCost = dto.OrderItems
                                    .Select(oi => new
                                    {
                                        Cost = (items.FirstOrDefault(i => oi.ItemId == i.Id)?.Cost ?? 0) * oi.ItemOrderQuantity,
                                    })
                                    .Sum(i => i.Cost);
            exepectedCost += dto.OrderItems.Select(oi => new
            {
                Cost = (
                        (orderItems.Any(oiInner => oiInner.Id == oi.Id))
                        ? (orderItems.FirstOrDefault(oiInner => oiInner.Id == oi.Id).Item?.Cost ?? 0) * oi.ItemOrderQuantity
                        : 0)
            })
             .Sum(i => i.Cost);
            exepectedCost = (1 - (coupon.Discount/100M)) * exepectedCost;
            var dateBeforeUpdate = DateTime.Now;

            var dtoUpdated = orderService.UpdateOrder(dto);

            _orderRepository.Verify(o => o.Update(It.IsAny<Domain.Model.Order>()), times: Times.Exactly(1));
            _orderItemService.Verify(o => o.DeleteOrderItem(It.IsAny<int>()), times: Times.Exactly(2));
            var dateAfterUpdate = DateTime.Now;
            dtoUpdated.Should().NotBeNull();
            dtoUpdated.Cost.Should().Be(exepectedCost);
            dtoUpdated.Number.Should().Be(dto.OrderNumber);
            dtoUpdated.Ordered.Should().Be(dto.Ordered);
            dtoUpdated.IsDelivered.Should().BeTrue();
            dtoUpdated.Delivered.Should().NotBeNull();
            dtoUpdated.Delivered.Value.Should().BeBefore(dateAfterUpdate);
            dtoUpdated.Delivered.Value.Should().BeAfter(dateBeforeUpdate);
            dtoUpdated.OrderItems.Should().NotBeEmpty();
            dtoUpdated.OrderItems.Count.Should().Be(dto.OrderItems.Count());
            dtoUpdated.CouponUsedId.Should().NotBeNull();
        }

        [Fact]
        public void given_used_coupon_when_update_order_should_throw_an_exception()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            _couponUsedRepository.Setup(c => c.Delete(It.IsAny<int>())).Returns(true);
            _orderItemService.Setup(oi => oi.GetOrderItemsNotOrdered(It.IsAny<IEnumerable<int>>())).Returns(new List<OrderItemDto>());
            var items = GenerateAndAddItems();
            order.CouponUsed = CreateDefaultCouponUsed(order.Id);
            order.CouponUsedId = order.CouponUsed.Id;
            var coupon = CreateDefaultCoupon();
            AddCoupon(coupon);
            GenerateIdsOnAddOrderItem();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 0, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                },
                PromoCode = coupon.Code,
            };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Cannot assign used coupon");
        }

        [Fact]
        public void given_order_items_not_connected_with_current_when_update_order_should_throw_an_exception()
        {
            var orderService = CreateService();
            var order = CreateDefaultOrder();
            var customer = CreateDefaultCustomer();
            var orderItems = CreateDefaultOrderItems(order, 5);
            order.OrderItems = orderItems;
            AddOrder(order);
            AddCustomer(customer);
            var items = GenerateItems();
            var dto = new UpdateOrderDto
            {
                Id = order.Id,
                CustomerId = customer.Id,
                OrderNumber = Guid.NewGuid().ToString(),
                Ordered = DateTime.Now,
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto>()
                {
                    new AddOrderItemDto { Id = 10, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 20, ItemId = items[0].Id, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { Id = 30, ItemId = items[0].Id, ItemOrderQuantity = 10 },
                },
            };

            Action action = () => orderService.UpdateOrder(dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Order doesn't have order item with id '30'");
        }

        #region DataInitial

        private void GenerateIdsOnAddOrderItem()
        {
            // TODO: Use list as data store instead of mock
            var addedOrderItems = new List<OrderItemDto>();
            _orderItemService.Setup(oi => oi.AddOrderItem(It.IsAny<OrderItemDto>())).Returns((OrderItemDto dto) => {
                var addedOrderItem = addedOrderItems.FirstOrDefault(oi => oi.ItemId == dto.ItemId);
                if (addedOrderItem != null)
                {
                    return addedOrderItem.Id;
                }

                var id = new Random().Next(1, 99999);
                dto.Id = id;
                addedOrderItems.Add(dto);
                return id;
            });
        }

        private void AddOrder(Domain.Model.Order order)
        {
            var orders = _orderRepository.Object.GetAllOrders();
            var ordersList = orders.ToList();
            ordersList.Add(order);
            _orderRepository.Setup(o => o.GetAllOrders()).Returns(ordersList.AsQueryable());
            _orderRepository.Setup(o => o.GetOrderDetailsById(order.Id)).Returns(order);
            if (order.Customer is not null)
            {
                AddCustomer(order.Customer);
            }
            if (order.Payment is not null)
            {
                AddPayment(order.Payment);
            }
            if(order.OrderItems is not null && order.OrderItems.Any())
            {
                AddOrderItems(order.OrderItems);
            }
        }

        private void AddCustomer(Domain.Model.Customer customer)
        {
            _customerService.Setup(c => c.GetCustomer(customer.Id)).Returns(_mapper.Map<CustomerDetailsDto>(customer));
        }

        private void AddOrderItems(IEnumerable<OrderItem> items)
        {
            _orderRepository.Setup(o => o.GetAllOrderItems()).Returns(items.AsQueryable());
            var itemsToAdd = items.Where(oi => oi.Item is not null)?.Select(oi => oi.Item) ?? Enumerable.Empty<Domain.Model.Item>();
            foreach(var it in itemsToAdd)
            {
                AddItem(it);
            }
        }

        private void AddItem(Domain.Model.Item item)
        {
            _itemService.Setup(i => i.GetItemById(item.Id)).Returns(_mapper.Map<NewItemVm>(item));
            var allItems = _itemService.Object.GetAllItems() ?? new List<ItemDto>();
            allItems.Add(_mapper.Map<ItemDto>(item));
            _itemService.Setup(i => i.GetAllItems()).Returns(allItems);
            _itemService.Setup(i => i.GetItemDetails(item.Id)).Returns(_mapper.Map<ItemDetailsDto>(item));
            _itemService.Setup(i => i.GetItemById(item.Id)).Returns(_mapper.Map<NewItemVm>(item));
        }

        private void AddPayment(Payment payment)
        {
            _paymentRepository.Setup(p => p.GetById(payment.Id)).Returns(payment);
        }

        private void AddCoupon(Domain.Model.Coupon coupon)
        {
            _couponService.Setup(c => c.GetCouponByCode(coupon.Code)).Returns(_mapper.Map<CouponVm>(coupon));
            _couponRepository.Setup(c => c.GetById(coupon.Id)).Returns(coupon);
        }

        private static List<ItemDto> GenerateItems()
        {
            return new List<ItemDto>()
            {
                new ItemDto
                {
                    Id = 1,
                    Name = Guid.NewGuid().ToString(),
                    Cost = 100
                },
                new ItemDto
                {
                    Id = 2,
                    Name = Guid.NewGuid().ToString(),
                    Cost = 300
                },
                new ItemDto
                {
                    Id = 3,
                    Name = Guid.NewGuid().ToString(),
                    Cost = 1000
                },
                new ItemDto
                {
                    Id = 4,
                    Name = Guid.NewGuid().ToString(),
                    Cost = 100000
                },
                new ItemDto
                {
                    Id = 5,
                    Name = Guid.NewGuid().ToString(),
                    Cost = 500
                }
            };
        }

        private List<ItemDto> GenerateAndAddItems()
        {
            var items = GenerateItems();
            _itemService.Setup(i => i.GetAllItems()).Returns(items);
            items.ForEach(i => _itemRepository.Setup(service => service.GetById(i.Id)).Returns(_mapper.Map<Domain.Model.Item>(i)));
            var currentItems = _itemRepository.Object.GetAll().ToList();
            items.ForEach(i => currentItems.Add(_mapper.Map<Domain.Model.Item>(i)));
            _itemRepository.Setup(i => i.GetAll()).Returns(currentItems.AsQueryable());
            return items;
        }

        private static Domain.Model.Customer CreateDefaultCustomer()
        {
            return new Domain.Model.Customer
            {
                Id = new Random().Next(1, 9999),
                FirstName = nameof(Domain.Model.Customer.FirstName),
                LastName = nameof(Domain.Model.Customer.LastName),
                IsCompany = true,
                CompanyName = nameof(Domain.Model.Customer.CompanyName),
                NIP = nameof(Domain.Model.Customer.NIP),
                UserId = Guid.NewGuid().ToString(),
                Addresses = new List<Address>
                {
                    new Address
                    {
                        Id = new Random().Next(1, 9999),
                        City = "NS",
                        Country = "Lubuskie",
                        FlatNumber = new Random().Next(1, 9999),
                        Street = "Harcerska",
                        ZipCode = 67100,
                        BuildingNumber = new Random().Next(1, 9999).ToString(),
                    }
                },
                ContactDetails = new List<ContactDetail>
                {
                    new ContactDetail
                    {
                        Id = new Random().Next(1, 9999),
                        ContactDetailInformation = new Random().Next(111111111, 999999999).ToString(),
                        ContactDetailTypeId = 1,
                    }
                },
            };
        }

        private static CouponUsed CreateDefaultCouponUsed(int? orderId = null)
        {
            var coupon = CreateDefaultCoupon();
            return new()
            {
                Id = new Random().Next(1, 9999),
                Coupon = coupon,
                CouponId = coupon.Id,
                OrderId = orderId.HasValue ? orderId.Value : 0
            };
        }

        private static Domain.Model.Order CreateDefaultOrder()
        {
            var order = new Domain.Model.Order
            {
                Id = new Random().Next(1, 9999),
                Number = "1234557567",
                Cost = new decimal(100),
                Ordered = DateTime.Now,
                IsPaid = false,
                IsDelivered = false,
                Delivered = null,
                CustomerId = 1,
                CurrencyId = 1,
                UserId = Guid.NewGuid().ToString(),
                OrderItems = new List<OrderItem>(),
                Currency = new Currency() { Id = 1 },
                Customer = new Domain.Model.Customer() { Id = 1 }
            };
            order.User = new ApplicationUser { Id = order.UserId };
            return order;
        }

        private static List<Domain.Model.Order> CreateDefaultOrders(int ordersCount = 3, Func<Domain.Model.Order, Domain.Model.Order> selector = null)
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

        private static List<OrderItem> CreateDefaultOrderItems(Domain.Model.Order order, int orderItemCount = 3)
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

        private static OrderItem CreateDefaultOrderItem(Domain.Model.Order order)
        {
            var orderItem = new OrderItem
            {
                Id = 1,
                UserId = order.UserId,
                User = order.User,
                OrderId = order.Id,
                Order = order
            };
            var itemId = new Random().Next(1, 9999);
            orderItem.ItemId = itemId;
            orderItem.Item = new Domain.Model.Item { Id = itemId, Cost = new Random().Next(1, 9999) };
            orderItem.ItemOrderQuantity = 1;
            return orderItem;
        }

        private static NewOrderVm CreateDefaultNewOrderVm()
        {
            var order = new NewOrderVm()
            {
                Id = 1,
                Cost = decimal.One,
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

        private static CouponType CreateDefaultCouponType()
        {
            var couponType = new CouponType()
            {
                Id = new Random().Next(1, 9999),
                Type = "Coupon"
            };
            return couponType;
        }

        private static Domain.Model.Coupon CreateDefaultCoupon()
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
                Cost = decimal.One,
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
