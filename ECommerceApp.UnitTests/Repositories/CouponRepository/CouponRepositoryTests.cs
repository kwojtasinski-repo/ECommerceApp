using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.CouponRepository
{
    public class CouponRepositoryTests : BaseTest<Coupon>
    {
        private readonly ICouponRepository _couponRepository;

        public CouponRepositoryTests()
        {
            _couponRepository = new Infrastructure.Repositories.CouponRepository(_context);
        }

        [Fact]
        public void CanReturnCouponById()
        {
            var id = 1;

            var couponThatExists = _couponRepository.GetById(id);

            couponThatExists.Should().NotBeNull();
            couponThatExists.Should().BeOfType(typeof(Coupon));
        }

        [Fact]
        public void CanReturnCouponByIdUsedInOrder()
        {
            var id = 2;

            var couponThatExists = _couponRepository.GetCouponById(id);

            couponThatExists.Should().NotBeNull();
            couponThatExists.Should().BeOfType(typeof(Coupon));
        }

        [Fact]
        public void CantReturnCouponById()
        {
            var id = 10;

            var couponThatExists = _couponRepository.GetById(id);

            couponThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCouponsFromDb()
        {
            var couponsThatExists = _couponRepository.GetAllCoupons().ToList();

            couponsThatExists.Should().NotBeNull();
            couponsThatExists.Should().HaveCount(3);
        }
    }
}
