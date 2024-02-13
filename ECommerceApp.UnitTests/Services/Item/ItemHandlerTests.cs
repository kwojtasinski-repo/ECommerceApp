using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Item
{
    public class ItemHandlerTests
    {
        private readonly ItemHandler _handler;
        private readonly Mock<IItemRepository> _itemRepository; 

        public ItemHandlerTests()
        {
            _itemRepository = new Mock<IItemRepository>();
            _handler = new ItemHandler(_itemRepository.Object, Mock.Of<ILogger<ItemHandler>>());
            AddItem(CreateItem());
        }

        [Fact]
        public void given_order_before_change_and_after_with_more_quantity_ordered_when_handle_items_changes_on_order_should_decrease_item_quantity()
        {
            var orderBeforeChange = WithOrderItems(CreateOrder());
            var orderAfterChange = WithOrderItems(CreateOrder());
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;
            orderItem.ItemOrderQuantity += 1;

            _handler.HandleItemsChangesOnOrder(orderBeforeChange, orderAfterChange);

            var itemAfterChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            itemAfterChange.Quantity.Should().BeLessThan(quantityBeforeChange);
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_order_before_change_and_after_with_no_changes_when_handle_items_changes_on_order_should_not_update_item()
        {
            var orderBeforeChange = WithOrderItems(CreateOrder());
            var orderAfterChange = WithOrderItems(CreateOrder());
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;

            _handler.HandleItemsChangesOnOrder(orderBeforeChange, orderAfterChange);

            var itemAfterChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            itemAfterChange.Quantity.Should().Be(quantityBeforeChange);
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Never);
        }

        [Fact]
        public void given_order_before_change_and_after_with_less_quantity_ordered_when_handle_items_changes_on_order_should_increase_item_quantity()
        {
            var orderBeforeChange = WithOrderItems(CreateOrder());
            var orderAfterChange = WithOrderItems(CreateOrder());
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;
            orderItem.ItemOrderQuantity -= 1;

            _handler.HandleItemsChangesOnOrder(orderBeforeChange, orderAfterChange);

            var itemAfterChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            itemAfterChange.Quantity.Should().BeGreaterThan(quantityBeforeChange);
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_new_order_when_handle_changes_on_order_should_decrease_item_quantity()
        {
            var orderAfterChange = WithOrderItems(CreateOrder(), 1);
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;

            _handler.HandleItemsChangesOnOrder(null, orderAfterChange);

            var itemAfterChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            itemAfterChange.Quantity.Should().BeLessThan(quantityBeforeChange);
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_deleted_order_when_handle_changes_on_order_should_increase_item_quantity()
        {
            var orderAfterChange = WithOrderItems(CreateOrder(), 1);
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;

            _handler.HandleItemsChangesOnOrder(orderAfterChange, null);

            var itemAfterChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            itemAfterChange.Quantity.Should().BeGreaterThan(quantityBeforeChange);
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_new_order_with_more_quantity_than_in_stock_when_handle_changes_on_order_should_throw_an_exception()
        {
            var orderAfterChange = WithOrderItems(CreateOrder(), 5);
            var orderItem = orderAfterChange.OrderItems.First();
            var itemBeforeChange = _itemRepository.Object.GetItemById(orderItem.ItemId);
            var quantityBeforeChange = itemBeforeChange.Quantity;

            var action = () => _handler.HandleItemsChangesOnOrder(null, orderAfterChange);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Should().Contain("that cannot be ordered with quantity of");
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Never);
        }

        [Fact]
        public void given_order_before_change_and_after_with_quantity_more_than_in_stock_when_handle_items_changes_on_order_should_throw_an_exception()
        {
            var orderBeforeChange = WithOrderItems(CreateOrder());
            var orderAfterChange = WithOrderItems(CreateOrder(), 10);

            var action = () => _handler.HandleItemsChangesOnOrder(orderBeforeChange, orderAfterChange);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Should().Contain("that cannot be ordered with quantity of");
            _itemRepository.Verify(i => i.UpdateItem(It.IsAny<Domain.Model.Item>()), Times.Never);
        }

        private void AddItem(Domain.Model.Item item)
        {
            _itemRepository.Setup(i => i.GetItemById(item.Id)).Returns(item);
            _itemRepository.Setup(i => i.GetItemDetailsById(item.Id)).Returns(item);
            var allItems = _itemRepository.Object.GetAllItems() ?? new List<Domain.Model.Item>();
            allItems.Add(item);
            _itemRepository.Setup(i => i.GetAllItems()).Returns(allItems);
            _itemRepository.Setup(i => i.GetItemsByIds(It.IsAny<IEnumerable<int>>())).Returns((IEnumerable<int> ids) => allItems.Where(item => ids.Any(id => item.Id == id)).ToList());
        }

        private static Order CreateOrder()
        {
            return new Order
            {
                Id = 1,
                Cost = 0M,
                Number = Guid.NewGuid().ToString("N"),
            };
        }

        private static Order WithOrderItems(Order order, int quantity = 2)
        {
            var orderItems = new List<Domain.Model.OrderItem>();
            var item = CreateItem();
            for (int i = 0; i < quantity; i++)
            {
                orderItems.Add(new Domain.Model.OrderItem { Id = i+1, ItemOrderQuantity = 2, ItemId = item.Id, Item = item });
            }
            order.OrderItems = orderItems;
            return order;
        }

        private static Domain.Model.Item CreateItem()
        {
            return new Domain.Model.Item
            {
                Id = 1,
                Quantity = 2
            };
        }
    }
}
