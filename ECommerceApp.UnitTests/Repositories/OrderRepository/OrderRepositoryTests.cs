using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests
{
    public class OrderRepositoryTests : BaseTest<Order>
    {
        private readonly IOrderRepository _orderRepository;

        public OrderRepositoryTests()
        {
            _orderRepository = new Infrastructure.Repositories.OrderRepository(_context);
        }

        [Fact]
        public void CanReturnOrderFromDb()
        {
            var id = 1;

            var orderThatExists = _orderRepository.GetOrderById(id);
            
            orderThatExists.Should().NotBeNull();
            orderThatExists.Should().BeOfType(typeof(Order));
        }

        [Fact]
        public void CantReturnOrderFromDb()
        {
            var id = 109;

            var orderThatExists = _orderRepository.GetOrderById(id);

            orderThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnOrdersFromDb()
        {
            var orders = new List<Order>();

            var ordersThatExists = _orderRepository.GetAllOrders().ToList();

            ordersThatExists.Should().NotBeNull();
            ordersThatExists.Count.Should().BeGreaterThan(orders.Count);
            ordersThatExists.Should().HaveCount(1);
        }
    }
}
