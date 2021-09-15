using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Coupon;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.CouponService
{
    public class CouponServiceTests : CouponBaseTest
    {
        [Fact]
        public void CanReturnCoupon()
        {
            var id = 1;

            var coupon = _service.Get(id);

            coupon.Should().NotBeNull();
            coupon.Should().BeOfType(typeof(CouponVm));
        }

        [Fact]
        public void ShouldAddCoupon()
        {
            var coupon = new CouponVm
            {
                Id = 0,
                Code = "JONOF143#lsa",
                Description = "Test123",
                Discount = 10
            };

            var id = _service.AddCoupon(coupon);

            id.Should().BeGreaterThan(3);
        }

        [Fact]
        public void ShouldntAddCoupon()
        {
            var coupon = new CouponVm { Id = 1000 };

            Action act = () => _service.AddCoupon(coupon);
            
            act.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }
    }
}
