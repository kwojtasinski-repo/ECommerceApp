using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.Tests.Services.Coupon
{
    public class CouponServiceTests : BaseTest
    {
        private readonly Mock<ICouponRepository> _couponRepository;

        public CouponServiceTests()
        {
            _couponRepository = new Mock<ICouponRepository>();
        }

        [Fact]
        public void given_coupon_should_add()
        {
            var coupon = CreateCoupon();
            coupon.Id = 0;
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            couponService.AddCoupon(coupon);

            _couponRepository.Verify(c => c.Add(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_should_add()
        {
            var coupon = CreateCoupon();
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.AddCoupon(coupon);

            act.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_invalid_coupon_discount_should_add()
        {
            var coupon = CreateCoupon();
            coupon.Id = 0;
            coupon.Discount = 1011;
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.AddCoupon(coupon);

            act.Should().ThrowExactly<BusinessException>().WithMessage("Discount should be inclusive between 1 and 99");
        }

        [Fact]
        public void given_valid_coupon_should_update()
        {
            var coupon = CreateCoupon();
            _couponRepository.Setup(c => c.ExistsById(coupon.Id)).Returns(true);
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            couponService.UpdateCoupon(coupon);

            _couponRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_should_update()
        {
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.UpdateCoupon(null);

            act.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_invalid_coupon_discount_should_update()
        {
            var coupon = CreateCoupon();
            coupon.Discount = 0;
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.UpdateCoupon(coupon);

            act.Should().ThrowExactly<BusinessException>().WithMessage("Discount should be inclusive between 1 and 99");
        }

        [Fact]
        public void given_valid_coupon_should_delete_coupon_used()
        {
            int couponId = 1;
            int couponUsedId = 1;
            var coupons = CreateCoupons();
            _couponRepository.Setup(c => c.GetAll()).Returns(coupons.AsQueryable());
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            couponService.DeleteCouponUsed(couponId, couponUsedId);

            _couponRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_should_delete_coupon_used()
        {
            int couponId = 1;
            int couponUsedId = 1;
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.DeleteCouponUsed(couponId, couponUsedId);

            act.Should().ThrowExactly<BusinessException>().WithMessage("Given invalid id");
        }

        [Fact]
        public void given_valid_coupon_should_add_coupon_used()
        {
            var couponId = 1;
            int couponUsedId = 1;
            var coupons = CreateCoupons();
            _couponRepository.Setup(c => c.GetAll()).Returns(coupons.AsQueryable());
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            couponService.AddCouponUsed(couponId, couponUsedId);

            _couponRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_should_add_coupon_used()
        {
            var couponId = 1;
            int couponUsedId = 1;
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action act = () => couponService.AddCouponUsed(couponId, couponUsedId);

            act.Should().ThrowExactly<BusinessException>().WithMessage("Given invalid id");
        }

        [Fact]
        public void given_null_coupon_when_add_should_throw_an_exception()
        {
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action action = () => couponService.AddCoupon(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_coupon_when_update_should_throw_an_exception()
        {
            var couponService = new CouponService(_couponRepository.Object, _mapper);

            Action action = () => couponService.UpdateCoupon(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static CouponVm CreateCoupon()
        {
            var coupon = new CouponVm
            {
                Id = 1,
                Code = "ADSGbdfheartwe",
                CouponTypeId = 1,
                Description = "",
                Discount = 10
            };
            return coupon;
        }

        private static List<Domain.Model.Coupon> CreateCoupons()
        {
            var coupons = new List<Domain.Model.Coupon>();
            Random random = new ();

            for (int i = 0; i < 3; i++)
            {
                var coupon = new Domain.Model.Coupon
                {
                    Id = i + 1,
                    Code = "ABC" + random.Next(100, 2000),
                    Description = "",
                    CouponTypeId = 1,
                    CouponUsedId = i + 1
                };

                coupons.Add(coupon);
            }

            return coupons;
        }
    }
}
