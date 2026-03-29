using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Coupons;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponsOrderCancelledHandlerTests
    {
        private readonly Mock<ICouponUsedRepository> _couponUsed;
        private readonly Mock<ICouponRepository> _coupons;

        public CouponsOrderCancelledHandlerTests()
        {
            _couponUsed = new Mock<ICouponUsedRepository>();
            _coupons = new Mock<ICouponRepository>();
        }

        private CouponsOrderCancelledHandler CreateHandler()
            => new(_couponUsed.Object, _coupons.Object);

        private static OrderCancelled CreateMessage(int orderId = 99)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        private static CouponUsed CreateCouponUsed(int id = 1, int couponId = 5, int orderId = 99)
        {
            var cu = CouponUsed.Create(new CouponId(couponId), orderId);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(id) });
            return cu;
        }

        private static Coupon CreateUsedCoupon(int id = 5)
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(id) });
            coupon.MarkAsUsed();
            return coupon;
        }

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_NoCouponUsedForOrder_ShouldBeNoOp()
        {
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CouponUsed?)null);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 99));

            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _coupons.Verify(r => r.UpdateAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Never);
            _couponUsed.Verify(r => r.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_CouponUsedExists_ShouldReleaseCouponAndPersist()
        {
            var couponUsed = CreateCouponUsed(couponId: 5, orderId: 99);
            var coupon = CreateUsedCoupon(id: 5);
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(couponUsed);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 99));

            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(couponUsed, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
