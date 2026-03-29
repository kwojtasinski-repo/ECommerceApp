using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponServiceTests
    {
        private readonly Mock<ICouponRepository> _coupons;
        private readonly Mock<ICouponUsedRepository> _couponUsed;
        private readonly Mock<IOrderExistenceChecker> _orderExistence;
        private readonly Mock<IMessageBroker> _broker;
        private readonly Mock<IScopeTargetRepository> _scopeTargets;
        private readonly Mock<ICouponRulePipeline> _pipeline;

        public CouponServiceTests()
        {
            _coupons = new Mock<ICouponRepository>();
            _couponUsed = new Mock<ICouponUsedRepository>();
            _orderExistence = new Mock<IOrderExistenceChecker>();
            _broker = new Mock<IMessageBroker>();
            _scopeTargets = new Mock<IScopeTargetRepository>();
            _pipeline = new Mock<ICouponRulePipeline>();
        }

        private ICouponService CreateService()
            => new CouponService(_coupons.Object, _couponUsed.Object, _orderExistence.Object, _broker.Object, _scopeTargets.Object, _pipeline.Object);

        private static Coupon CreateAvailableCoupon(int id = 1, string code = "SAVE10")
        {
            var coupon = Coupon.Create(code, "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(id) });
            return coupon;
        }

        private static CouponUsed CreateCouponUsed(int id = 1, int couponId = 1, int orderId = 99)
        {
            var cu = CouponUsed.Create(new CouponId(couponId), orderId);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(id) });
            return cu;
        }

        // ── ApplyCouponAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ApplyCouponAsync_OrderNotFound_ShouldReturnOrderNotFound()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.OrderNotFound);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponNotFound_ShouldReturnCouponNotFound()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("NOSUCH", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon?)null);

            var result = await CreateService().ApplyCouponAsync("NOSUCH", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.CouponNotFound);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponAlreadyUsed_ShouldReturnCouponAlreadyUsed()
        {
            var coupon = CreateAvailableCoupon();
            coupon.MarkAsUsed();
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.CouponAlreadyUsed);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_OrderAlreadyHasCoupon_ShouldReturnOrderAlreadyHasCoupon()
        {
            var coupon = CreateAvailableCoupon();
            var existing = CreateCouponUsed(orderId: 99);
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.OrderAlreadyHasCoupon);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_HappyPath_ShouldMarkCouponUsedPersistAndPublishCouponApplied()
        {
            var coupon = CreateAvailableCoupon(id: 5, code: "SAVE10");
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CouponUsed?)null);
            CouponUsed? savedCouponUsed = null;
            _couponUsed
                .Setup(x => x.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback<CouponUsed, CancellationToken>((cu, _) =>
                {
                    typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                        .GetSetMethod(nonPublic: true)!
                        .Invoke(cu, new object[] { new CouponUsedId(7) });
                    savedCouponUsed = cu;
                })
                .Returns(Task.CompletedTask);

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.Applied);
            coupon.Status.Should().Be(CouponStatus.Used);
            savedCouponUsed.Should().NotBeNull();
            savedCouponUsed!.CouponId.Value.Should().Be(5);
            savedCouponUsed.OrderId.Should().Be(99);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(CouponApplied) &&
                ((CouponApplied)m[0]).OrderId == 99 &&
                ((CouponApplied)m[0]).CouponUsedId == 7 &&
                ((CouponApplied)m[0]).DiscountPercent == 0)), Times.Once);
        }

        // ── RemoveCouponAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task RemoveCouponAsync_NoCouponApplied_ShouldReturnNoCouponApplied()
        {
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CouponUsed?)null);

            var result = await CreateService().RemoveCouponAsync(99);

            result.Should().Be(CouponRemoveResult.NoCouponApplied);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task RemoveCouponAsync_HappyPath_ShouldReleaseCouponPersistAndPublishCouponRemovedFromOrder()
        {
            var couponUsed = CreateCouponUsed(id: 3, couponId: 5, orderId: 99);
            var coupon = CreateAvailableCoupon(id: 5);
            coupon.MarkAsUsed();
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(couponUsed);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            var result = await CreateService().RemoveCouponAsync(99);

            result.Should().Be(CouponRemoveResult.Removed);
            coupon.Status.Should().Be(CouponStatus.Available);
            _couponUsed.Verify(r => r.DeleteAsync(couponUsed, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(CouponRemovedFromOrder) &&
                ((CouponRemovedFromOrder)m[0]).OrderId == 99)), Times.Once);
        }
    }
}
