using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Services.OrderItem
{
    public class OrderItemServiceTests : BaseTest
    {
        private readonly Mock<IOrderItemRepository> _orderItemRepository;

        public OrderItemServiceTests()
        {
            _orderItemRepository = new Mock<IOrderItemRepository>();
        }

        [Fact]
        public void given_valid_order_item_should_add()
        {
            int id = 0;
            var itemId = 1;
            var userId = Guid.NewGuid().ToString(); 
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.AddOrderItem(orderItem);

            _orderItemRepository.Verify(oi => oi.AddOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_item_when_add_should_throw_an_exception()
        {
            int id = 1;
            var itemId = 1;
            var userId = Guid.NewGuid().ToString();
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            Action action = () => orderItemService.AddOrderItem(orderItem);

            action.Should().Throw<BusinessException>().WithMessage("Given invalid orderItem");
        }

        [Fact]
        public void given_valid_order_item_should_add_count_to_exists_order_item()
        {
            int id = 1;
            var itemId = 1;
            var userId = Guid.NewGuid().ToString();
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemFromDb = CreateOrderItem(id, itemId, userId, quantity);
            _orderItemRepository.Setup(oi => oi.GetAllOrderItems()).Returns((new List<Domain.Model.OrderItem> { orderItemFromDb }).AsQueryable());
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.AddOrderItem(orderItem);

            _orderItemRepository.Verify(oi => oi.UpdateOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
            orderItemFromDb.ItemOrderQuantity.Should().BeGreaterThan(quantity);
        }

        [Fact]
        public void given_valid_id_order_item_should_exists()
        {
            var id = 1;
            var orderItem = CreateOrderItem(id, 1, "ab", 1);
            _orderItemRepository.Setup(oi => oi.GetAllOrderItems()).Returns(new List<Domain.Model.OrderItem> { orderItem }.AsQueryable());
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            var exists = orderItemService.OrderItemExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_id_order_item_shouldnt_exists()
        {
            var id = 1;
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            var exists = orderItemService.OrderItemExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_order_item_should_update()
        {
            var orderItem = new OrderItemDto { Id = 1, ItemId = 1, UserId = "gs", ItemOrderQuantity = 1 };
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.UpdateOrderItems(new List<OrderItemDto> { orderItem });

            _orderItemRepository.Verify(oi => oi.UpdateRange(It.IsAny<List<Domain.Model.OrderItem>>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_item_shouldnt_update()
        {
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.UpdateOrderItems(new List<OrderItemDto> { });

            _orderItemRepository.Verify(oi => oi.UpdateRange(It.IsAny<List<Domain.Model.OrderItem>>()), Times.Never);
        }

        [Fact]
        public void given_valid_order_with_user_id_order_item_should_add()
        {
            var itemId = 1;
            var userId = Guid.NewGuid().ToString();
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.AddOrderItem(itemId, userId);

            _orderItemRepository.Verify(oi => oi.AddOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
        }

        [Fact]
        public void given_valid_exists_order_with_user_id_order_item_should_add_count_to_quantity()
        {
            var id = 1;
            var itemId = 1;
            var userId = Guid.NewGuid().ToString();
            var quantity = 1;
            var orderItem = CreateOrderItem(id, itemId, userId, quantity);
            _orderItemRepository.Setup(oi => oi.GetAllOrderItems()).Returns(new List<Domain.Model.OrderItem> { orderItem }.AsQueryable());
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            orderItemService.AddOrderItem(itemId, userId);

            _orderItemRepository.Verify(oi => oi.UpdateOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
        }

        [Fact]
        public void given_null_order_item_when_add_should_throw_an_exception()
        {
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            Action action = () => orderItemService.AddOrderItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_order_item_when_update_should_throw_an_exception()
        {
            var orderItemService = new OrderItemService(_orderItemRepository.Object, _mapper);

            Action action = () => orderItemService.UpdateOrderItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static OrderItemDto CreateOrderItemDto(int id, int itemId, string userId, int quantity, int? orderId = null)
        {
            var orderItem = new OrderItemDto
            {
                Id = id,
                ItemId = itemId,
                OrderId = orderId,
                UserId = userId,
                ItemOrderQuantity = quantity
            };
            return orderItem;
        }

        private Domain.Model.OrderItem CreateOrderItem(int id, int itemId, string userId, int quantity, int? orderId = null)
        {
            var orderItem = new Domain.Model.OrderItem
            {
                Id = id,
                ItemId = itemId,
                OrderId = orderId,
                UserId = userId,
                ItemOrderQuantity = quantity
            };
            return orderItem;
        }
    }
}
