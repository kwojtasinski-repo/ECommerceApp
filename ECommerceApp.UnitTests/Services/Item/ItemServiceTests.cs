using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using Moq;
using System;
using Xunit;
using FluentAssertions;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.UnitTests.Common;

namespace ECommerceApp.Tests.Services.Item
{
    public class ItemServiceTests : BaseTest
    {
        private readonly Mock<IItemRepository> _itemRepository;
        private readonly Mock<ITagRepository> _tagRepository;

        public ItemServiceTests()
        {
            _itemRepository = new Mock<IItemRepository>();
            _tagRepository = new Mock<ITagRepository>();
        }

        public ItemService CreateItemService()
            => new ItemService(_itemRepository.Object, _mapper, _tagRepository.Object);

        [Fact]
        public void given_valid_item_should_add()
        {
            var item = new ItemVm { Id = 0, Cost = decimal.One, BrandId = 1, CurrencyId = 1, Name = "Item 1", Quantity = 10, TypeId = 1, Warranty = "100", Description = "ABC" };
            var itemService = CreateItemService();

            itemService.Add(item);

            _itemRepository.Verify(i => i.Add(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_invalid_item_should_throw_an_exception()
        {
            var item = new ItemVm { Id = 10 };
            var itemService = CreateItemService();

            Action action = () => { itemService.Add(item); };

            Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_valid_item_id_should_exists()
        {
            int id = 1;
            _itemRepository.Setup(i => i.ItemExists(id)).Returns(true);
            var itemService = CreateItemService();

            var exists = itemService.ItemExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_item_id_shouldnt_exists()
        {
            int id = 1;
            var itemService = CreateItemService();

            var exists = itemService.ItemExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_item_when_add_should_throw_an_exception()
        {
            var itemService = CreateItemService();

            Action action = () => itemService.AddItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_item_when_update_should_throw_an_exception()
        {
            var itemService = CreateItemService();

            Action action = () => itemService.UpdateItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }
    }
}
