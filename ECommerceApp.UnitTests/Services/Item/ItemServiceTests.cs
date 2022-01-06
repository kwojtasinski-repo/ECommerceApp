using AutoMapper;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using FluentAssertions;
using ECommerceApp.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Infrastructure.Repositories;

namespace ECommerceApp.Tests.Services.Item
{
    public class ItemServiceTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<IItemRepository> _itemRepository;

        public ItemServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _itemRepository = new Mock<IItemRepository>();
        }

        [Fact]
        public void given_valid_item_should_add()
        {
            var item = new ItemVm { Id = 0, Cost = decimal.One, BrandId = 1, CurrencyId = 1, Name = "Item 1", Quantity = 10, TypeId = 1, Warranty = "100", Description = "ABC" };
            var itemService = new Application.Services.ItemService(_itemRepository.Object, _mapper);

            itemService.Add(item);

            _itemRepository.Verify(i => i.Add(It.IsAny<Domain.Model.Item>()), Times.Once);
        }

        [Fact]
        public void given_invalid_item_should_throw_an_exception()
        {
            var item = new ItemVm { Id = 10 };
            var itemService = new Application.Services.ItemService(_itemRepository.Object, _mapper);

            Action action = () => { itemService.Add(item); };

            Assert.Throws<BusinessException>(action);
        }

        [Fact]
        public void given_valid_item_id_should_exists()
        {
            int id = 1;
            _itemRepository.Setup(i => i.ItemExists(id)).Returns(true);
            var itemService = new Application.Services.ItemService(_itemRepository.Object, _mapper);

            var exists = itemService.ItemExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_item_id_shouldnt_exists()
        {
            int id = 1;
            var itemService = new Application.Services.ItemService(_itemRepository.Object, _mapper);

            var exists = itemService.ItemExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_item_when_add_should_throw_an_exception()
        {
            var itemService = new ItemService(_itemRepository.Object, _mapper);

            Action action = () => itemService.AddItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_item_when_update_should_throw_an_exception()
        {
            var itemService = new ItemService(_itemRepository.Object, _mapper);

            Action action = () => itemService.UpdateItem(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }
    }
}
