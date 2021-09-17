using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.OrderService
{
    public class OrderServiceTests : BaseServiceTest<OrderVm, IOrderRepository, OrderRepository, Application.Services.OrderService, Order>
    {
        [Fact]
        public void CanReturnOrder()
        {
            var id = 1;

            var coupon = _service.Get(id);

            coupon.Should().NotBeNull();
            coupon.Should().BeOfType(typeof(OrderVm));
        }

        [Fact]
        public void ShouldAddOrder()
        {
            var order = new OrderVm
            {
                Id = 0,
                CustomerId = 1,
                IsPaid = true,
                IsDelivered = false
            };

            var id = _service.Add(order);
            var itemFromDb = _context.Orders.Where(i => i.Id == id).AsNoTracking().FirstOrDefault();

            itemFromDb.Should().NotBeNull();
            itemFromDb.Id.Should().Be(id);
        }

        [Fact]
        public void ShouldntAddOrder()
        {
            var order = new OrderVm { Id = 1000 };

            Action act = () => _service.Add(order);

            act.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }
    }
}
