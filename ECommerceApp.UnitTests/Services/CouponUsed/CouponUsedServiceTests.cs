using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Services.CouponUsed
{
    public class CouponUsedServiceTests : BaseTest
    {
        private readonly Mock<ICouponUsedRepository> _couponUsedRepository;
        private readonly Mock<IOrderService> _orderService;
        private readonly Mock<ICouponService> _couponService;

        public CouponUsedServiceTests()
        {
            _couponUsedRepository = new Mock<ICouponUsedRepository>();
            _orderService = new Mock<IOrderService>();
            _couponService = new Mock<ICouponService>();
        }

        [Fact]
        public void given_valid_coupon_used_should_add()
        {
            int id = 1;
            int couponId = 1;
            int orderId = 1;
            var couponUsed = CreateCouponUsedVm(id, couponId, orderId);
            couponUsed.Id = 0;
            _couponUsedRepository.Setup(cu => cu.AddCouponUsed(It.IsAny<Domain.Model.CouponUsed>())).Returns(1);
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            couponUsedService.AddCouponUsed(couponUsed);

            _couponUsedRepository.Verify(cu => cu.AddCouponUsed(It.IsAny<Domain.Model.CouponUsed>()), Times.Once);
            _orderService.Verify(o => o.AddCouponUsedToOrder(orderId, It.IsAny<int>()), Times.Once);
            _couponService.Verify(o => o.AddCouponUsed(orderId, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void given_invalid_coupon_used_should_throw_an_exception()
        {
            int id = 1;
            int couponId = 1;
            int orderId = 1;
            var couponUsed = CreateCouponUsedVm(id, couponId, orderId);
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            Action action = () => couponUsedService.AddCouponUsed(couponUsed);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_coupon_id_should_delete_coupon_used()
        {
            var coupons = CreateCouponsUsed();
            var coupon = coupons.First();
            var couponId = coupon.Id;
            _couponUsedRepository.Setup(cu => cu.GetAll()).Returns(coupons.AsQueryable());
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            couponUsedService.DeleteCouponUsed(couponId);

            _orderService.Verify(o => o.DeleteCouponUsedFromOrder(coupon.OrderId, couponId), Times.Once);
            _couponService.Verify(c => c.DeleteCouponUsed(coupon.CouponId, couponId), Times.Once);
            _couponUsedRepository.Verify(cu => cu.Delete(couponId));
        }

        [Fact]
        public void given_not_existing_coupon_id_when_delete_should_return_false()
        {
            var couponId = 1;
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            var deleted = couponUsedService.DeleteCouponUsed(couponId);

            deleted.Should().BeFalse();
        }

        [Fact]
        public void given_valid_coupon_should_update()
        {
            var coupon = CreateCouponUsedVm(1, 1, 1);
            _couponUsedRepository.Setup(c => c.ExistsById(coupon.Id)).Returns(true);
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            couponUsedService.UpdateCouponUsed(coupon);

            _couponUsedRepository.Verify(cu => cu.UpdateCouponUsed(It.IsAny<Domain.Model.CouponUsed>()), Times.Once);
        }

        [Fact]
        public void given_null_coupon_used_when_add_should_throw_an_exception()
        {
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            Action action = () => couponUsedService.AddCouponUsed(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_coupon_used_when_update_should_throw_an_exception()
        {
            var couponUsedService = new CouponUsedService(_couponUsedRepository.Object, _mapper, _orderService.Object, _couponService.Object);

            Action action = () => couponUsedService.UpdateCouponUsed(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static CouponUsedVm CreateCouponUsedVm(int id, int couponId, int orderId)
        {
            var couponUsed = new CouponUsedVm
            {
                Id = id,
                CouponId = couponId,
                OrderId = orderId
            };
            return couponUsed;
        }

        private static List<Domain.Model.CouponUsed> CreateCouponsUsed()
        {
            var couponsUsed = new List<Domain.Model.CouponUsed>
            {
                CreateCouponUsed(1, 1, 1),
                CreateCouponUsed(2, 2, 2),
                CreateCouponUsed(3, 3, 3)
            };
            return couponsUsed;
        }

        private static Domain.Model.CouponUsed CreateCouponUsed(int id, int couponId, int orderId)
        {
            var couponUsed = new Domain.Model.CouponUsed
            {
                Id = id,
                CouponId = couponId,
                OrderId = orderId
            };
            return couponUsed;
        }
    }
}
