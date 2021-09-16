using System;
using System.Collections.Generic;
using System.Text;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using Xunit;
using System.Collections.ObjectModel;
using System.Linq;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Tests.Common;

namespace ECommerceApp.Tests.Repositories.ItemRepository
{
    public class ItemRepositoryTests : BaseTest<Item>
    {
        private readonly IItemRepository _itemRepository;

        public ItemRepositoryTests()
        {
            _itemRepository = new Infrastructure.Repositories.ItemRepository(_context);
        }

        [Fact]
        public void CanReturnItemFromDb()
        {
            var id = 1;

            var itemThatExists = _itemRepository.GetItemById(id);

            itemThatExists.Should().NotBeNull();
            itemThatExists.Should().BeOfType(typeof(Item));
        }

        [Fact]
        public void CantReturnItemFromDb()
        {
            var id = 1123;

            var itemThatExists = _itemRepository.GetItemById(id);

            itemThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnItemsFromDb()
        {
            var items = new List<Item>();

            var itemsThatExists = _itemRepository.GetAllItems().ToList();

            itemsThatExists.Should().NotBeNull();
            itemsThatExists.Count.Should().BeGreaterThan(items.Count);
            itemsThatExists.Should().HaveCount(2);
        }
    }
}
