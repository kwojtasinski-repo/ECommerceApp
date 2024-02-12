using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Services.OrderItem
{
    public class OrderItemServiceTests : BaseTest
    {
        private readonly Mock<IOrderItemRepository> _orderItemRepository;
        private readonly Mock<IItemRepository> _itemRepository;
        private const int ItemId = 1;

        public OrderItemServiceTests()
        {
            _orderItemRepository = new Mock<IOrderItemRepository>();
            _itemRepository = new Mock<IItemRepository>();
            AddItem(new Item { Id = ItemId, Quantity = 20 });
        }

        private OrderItemService CreateService()
            => new (_orderItemRepository.Object, _mapper, _itemRepository.Object);

        [Fact]
        public void given_valid_order_item_should_add()
        {
            int id = 0;
            var itemId = ItemId;
            var userId = Guid.NewGuid().ToString(); 
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemService = CreateService();

            orderItemService.AddOrderItem(orderItem);

            _orderItemRepository.Verify(oi => oi.AddOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_item_when_add_should_throw_an_exception()
        {
            int id = 1;
            var itemId = ItemId;
            var userId = Guid.NewGuid().ToString();
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemService = CreateService();

            Action action = () => orderItemService.AddOrderItem(orderItem);

            action.Should().Throw<BusinessException>().WithMessage($"Check if your position with id '{orderItem.Id}' with item with id '{orderItem.ItemId}' is in cart");
        }

        [Fact]
        public void given_valid_order_item_should_add_count_to_exists_order_item()
        {
            int id = 1;
            var itemId = ItemId;
            var userId = Guid.NewGuid().ToString();
            var quantity = 1;
            var orderItem = CreateOrderItemDto(id, itemId, userId, quantity);
            var orderItemFromDb = CreateOrderItem(id, itemId, userId, quantity);
            _orderItemRepository.Setup(oi => oi.GetUserOrderItemNotOrdered(userId, itemId)).Returns(orderItemFromDb);
            var orderItemService = CreateService();

            orderItemService.AddOrderItem(orderItem);

            _orderItemRepository.Verify(oi => oi.UpdateOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
            orderItemFromDb.ItemOrderQuantity.Should().BeGreaterThan(quantity);
        }

        [Fact]
        public void given_valid_id_order_item_should_exists()
        {
            var id = 1;
            _orderItemRepository.Setup(oi => oi.ExistsById(id)).Returns(true);
            var orderItemService = CreateService();

            var exists = orderItemService.OrderItemExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_id_order_item_shouldnt_exists()
        {
            var id = 1;
            var orderItemService = CreateService();

            var exists = orderItemService.OrderItemExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_order_item_should_update()
        {
            var orderItem = new OrderItemDto { Id = 1, ItemId = 1, UserId = "gs", ItemOrderQuantity = 1 };
            var orderItemService = CreateService();

            orderItemService.UpdateOrderItems(new List<OrderItemDto> { orderItem });

            _orderItemRepository.Verify(oi => oi.UpdateRange(It.IsAny<List<Domain.Model.OrderItem>>()), Times.Once);
        }

        [Fact]
        public void given_invalid_order_item_shouldnt_update()
        {
            var orderItemService = CreateService();

            orderItemService.UpdateOrderItems(new List<OrderItemDto> { });

            _orderItemRepository.Verify(oi => oi.UpdateRange(It.IsAny<List<Domain.Model.OrderItem>>()), Times.Never);
        }

        [Fact]
        public void given_valid_order_with_user_id_order_item_should_add()
        {
            var itemId = ItemId;
            var userId = Guid.NewGuid().ToString();
            var orderItemService = CreateService();

            orderItemService.AddOrderItem(itemId, userId);

            _orderItemRepository.Verify(oi => oi.AddOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Once);
        }

        [Fact]
        public void given_null_order_item_when_add_should_throw_an_exception()
        {
            var orderItemService = CreateService();

            Action action = () => orderItemService.AddOrderItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_order_item_when_update_should_throw_an_exception()
        {
            var orderItemService = CreateService();

            Action action = () => orderItemService.UpdateOrderItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_not_existing_item_when_add_order_item_should_throw_an_exception()
        {
            var service = CreateService();

            var action = () => service.AddOrderItem(new OrderItemDto { ItemId = 100 });

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("was not found");
        }

        [Fact]
        public void given_item_not_available_item_when_add_order_item_should_throw_an_exception()
        {
            var id = 2;
            AddItem(new Item { Id = id, Quantity = 0 });
            var service = CreateService();

            var action = () => service.AddOrderItem(new OrderItemDto { ItemId = id, ItemOrderQuantity = 1 });

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains(" is not available");
        }

        [Fact]
        public void given_quantity_more_than_in_stock_when_add_order_item_should_throw_an_exception()
        {
            var id = 2;
            AddItem(new Item { Id = id, Quantity = 1 });
            var service = CreateService();
            
            var action = () => service.AddOrderItem(new OrderItemDto { ItemId = id, ItemOrderQuantity = 2 });

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be ordered with quantity of");

        }

        [Fact]
        public void given_not_existing_order_item_when_update_should_throw_an_exception()
        {
            var id = 2;
            var service = CreateService();

            var result = service.UpdateOrderItem(new OrderItemDto { ItemId = id, ItemOrderQuantity = 2 });

            result.Should().Be(false);
            _orderItemRepository.Verify(oi => oi.UpdateOrderItem(It.IsAny<Domain.Model.OrderItem>()), Times.Never);
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

        private static Domain.Model.OrderItem CreateOrderItem(int id, int itemId, string userId, int quantity, int? orderId = null)
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

        private void AddItem(Item item)
        {
            _itemRepository.Setup(i => i.GetItemById(item.Id)).Returns(item);
        }
    }
}
