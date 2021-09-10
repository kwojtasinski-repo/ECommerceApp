using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.AbstractRepository
{
    public class ImageAbstractRepositoryTests : BaseTest<Image>
    {
        [Fact]
        public void CanGetImageById()
        {
            var id = 1;

            var image = _abstractRepository.GetById(id);

            image.Should().NotBeNull();
        }
    }
}
