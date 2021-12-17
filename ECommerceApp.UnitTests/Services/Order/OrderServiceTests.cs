using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
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

        private Domain.Model.Order GetDefaultOrder()
        {
            var order = new Domain.Model.Order();
            order.Id = 1;
            order.Number = 12345;
            order.Cost = new decimal(100);
            order.Ordered = DateTime.Now;
            order.IsPaid = false;
            order.IsDelivered = false;
            order.Delivered = null;
            order.CustomerId = 1;
            order.CurrencyId = 1;
            order.UserId = Guid.NewGuid().ToString();
            return order;
        }
    }
}
