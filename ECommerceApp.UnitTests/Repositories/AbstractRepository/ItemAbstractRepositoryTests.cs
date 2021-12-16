using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.AbstractRepository
{
    public class ItemAbstractRepositoryTests : BaseTest<Item>
    {
        [Fact]
        public void CanGetItemById()
        {
            var id = 1;

            var item = _abstractRepository.GetById(id);

            item.Should().NotBeNull();
        }
    }
}
