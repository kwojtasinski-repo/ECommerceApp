﻿using AutoMapper;
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
using ECommerceApp.Tests.Common;
using ECommerceApp.Infrastructure.Repositories;

namespace ECommerceApp.Tests.Services.ItemService
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

            _itemRepository.Verify(i => i.Add(It.IsAny<Item>()), Times.Once);
        }

        [Fact]
        public void given_invalid_item_should_throw_an_exception()
        {
            var item = new ItemVm { Id = 10 };
            var itemService = new Application.Services.ItemService(_itemRepository.Object, _mapper);

            Action action = () => { itemService.Add(item); };

            Assert.Throws<BusinessException>(action);
        }
    }
}
