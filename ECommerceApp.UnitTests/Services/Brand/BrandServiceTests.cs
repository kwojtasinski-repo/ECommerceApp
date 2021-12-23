using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Brand
{
    public class BrandServiceTests
    {
        private readonly Mock<IBrandRepository> _brandRepository;
        private readonly IMapper _mapper;

        public BrandServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
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
