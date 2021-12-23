using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.UnitTests.Services.CouponType
{
    public class CouponTypeTests
    {
        private readonly Mock<ICouponTypeRepository> _couponTypeRepository;
        private readonly IMapper _mapper;

        public CouponTypeTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _couponTypeRepository = new Mock<ICouponTypeRepository>();
        }

        [Fact]
        public void given_valid_coupon_type_should_add()
        {
            int id = 0;
            var coupon = CreateCouponTypeVm(id);
            var couponTypeService = new CouponTypeService(_couponTypeRepository.Object, _mapper);

            couponTypeService.AddCouponType(coupon);

            _couponTypeRepository.Verify(ct => ct.AddCouponType(It.IsAny<Domain.Model.CouponType>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_type_shoudlnt_add()
        {
            int id = 1;
            var coupon = CreateCouponTypeVm(id);
            var couponTypeService = new CouponTypeService(_couponTypeRepository.Object, _mapper);

            Action action = () => couponTypeService.AddCouponType(coupon);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        private CouponTypeVm CreateCouponTypeVm(int id)
        {
            var couponType = new CouponTypeVm
            {
                Id = id,
                Type = "type"
            };
            return couponType;
        }
    }
}
