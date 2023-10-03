using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Brand
{
    public class BrandServiceTests : BaseTest
    {
        private readonly Mock<IBrandRepository> _brandRepository;

        public BrandServiceTests()
        {
            _brandRepository = new Mock<IBrandRepository>();
        }

        [Fact]
        public void given_valid_brand_should_add()
        {
            var brand = CreateBrand();
            brand.Id = 0;
            var brandService = new BrandService(_brandRepository.Object, _mapper);

            brandService.AddBrand(brand);

            _brandRepository.Verify(b => b.Add(It.IsAny<Domain.Model.Brand>()), Times.Once);
        }

        [Fact]
        public void given_invalid_brand_should_add()
        {
            var brand = CreateBrand();
            var brandService = new BrandService(_brandRepository.Object, _mapper);

            Action action = () => brandService.AddBrand(brand);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_null_brand_when_add_should_throw_an_exception()
        {
            var brandService = new BrandService(_brandRepository.Object, _mapper);

            Action action = () => brandService.AddBrand(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_brand_when_update_should_throw_an_exception()
        {
            var brandService = new BrandService(_brandRepository.Object, _mapper);

            Action action = () => brandService.AddBrand(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private BrandVm CreateBrand()
        {
            var brand = new BrandVm
            {
                Id = 1,
                Name = "Name"
            };
            return brand;
        }
    }
}
