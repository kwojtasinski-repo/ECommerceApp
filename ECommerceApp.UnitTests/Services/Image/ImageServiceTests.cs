using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.ImageService
{
    public class ImageServiceTests : BaseServiceTest<ImageVm, IImageRepository, ImageRepository, Application.Services.ImageService, Domain.Model.Image>
    {
        [Fact]
        public void CanGetImageById()
        {
            var id = 1;

            var image = _service.Get(id);

            image.Should().NotBeNull();
        }
    }
}
