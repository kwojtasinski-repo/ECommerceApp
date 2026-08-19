using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Coupons;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponsPaymentExpiredHandlerTests
    {
        private readonly Mock<ICouponUsedRepository> _couponUsed;
        private readonly Mock<ICouponRepository> _coupons;
        private readonly Mock<ICouponApplicationRecordRepository> _applicationRecords;

        public CouponsPaymentExpiredHandlerTests()
        {
            _couponUsed = new Mock<ICouponUsedRepository>();
            _coupons = new Mock<ICouponRepository>();
            _applicationRecords = new Mock<ICouponApplicationRecordRepository>();
        }

        private CouponsPaymentExpiredHandler CreateHandler()
            => new(_couponUsed.Object, _coupons.Object, _applicationRecords.Object, NullLogger<CouponsPaymentExpiredHandler>.Instance);

        private static PaymentExpired CreateMessage(int orderId = 99)
            => new(PaymentId: 10, OrderId: orderId, OccurredAt: DateTime.UtcNow);

        private static CouponUsed CreateCouponUsed(int id = 1, int couponId = 5, int orderId = 99)
        {
            var cu = CouponUsed.Create(new CouponId(couponId), orderId);
            EntityIdSetter.Set(cu, new CouponUsedId(id));
            return cu;
        }

        private static Coupon CreateUsedCoupon(int id = 5)
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            EntityIdSetter.Set(coupon, new CouponId(id));
            coupon.MarkAsUsed();
            return coupon;
        }

        private void SetupCouponUsedForExpiredOrder(CouponUsed couponUsed, Coupon coupon)
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { couponUsed });
            _coupons.Setup(x => x.GetByIdAsync(couponUsed.CouponId!.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupon);
        }

            private void SetupNoCouponUsedForExpiredOrder()
            {
                _couponUsed
                .Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed>());
            }

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_NoCouponUsedForOrder_ShouldBeNoOp()
        {
            // Arrange
            SetupNoCouponUsedForExpiredOrder();

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 99), TestContext.Current.CancellationToken);

            // Assert
            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _coupons.Verify(r => r.UpdateAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Never);
            _couponUsed.Verify(r => r.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_CouponUsedExists_ShouldReleaseCouponAndPersist()
        {
            // Arrange
            var couponUsed = CreateCouponUsed(couponId: 5, orderId: 99);
            var coupon = CreateUsedCoupon(id: 5);
            SetupCouponUsedForExpiredOrder(couponUsed, coupon);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 99), TestContext.Current.CancellationToken);

            // Assert
            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(couponUsed, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
