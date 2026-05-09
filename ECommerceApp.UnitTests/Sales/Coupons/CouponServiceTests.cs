using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using AwesomeAssertions;
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
        private readonly Mock<ICouponApplicationRecordRepository> _applicationRecords;
        private readonly CouponsOptions _options;

        public CouponServiceTests()
        {
            _coupons = new Mock<ICouponRepository>();
            _couponUsed = new Mock<ICouponUsedRepository>();
            _orderExistence = new Mock<IOrderExistenceChecker>();
            _broker = new Mock<IMessageBroker>();
            _scopeTargets = new Mock<IScopeTargetRepository>();
            _pipeline = new Mock<ICouponRulePipeline>();
            _applicationRecords = new Mock<ICouponApplicationRecordRepository>();
            _options = new CouponsOptions();
        }

        private ICouponService CreateService()
            => new CouponService(_coupons.Object, _couponUsed.Object, _orderExistence.Object, _broker.Object, _scopeTargets.Object, _pipeline.Object, _options, _applicationRecords.Object);

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

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

            result.Should().Be(CouponApplyResult.OrderNotFound);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponNotFound_ShouldReturnCouponNotFound()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("NOSUCH", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);

            var result = await CreateService().ApplyCouponAsync("NOSUCH", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

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

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

            result.Should().Be(CouponApplyResult.CouponAlreadyUsed);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_OrderAlreadyHasCoupon_ShouldReturnOrderAlreadyHasCoupon()
        {
            var coupon = CreateAvailableCoupon();
            var existingCoupons = new List<CouponUsed>();
            for (int i = 0; i < 5; i++)
                existingCoupons.Add(CreateCouponUsed(id: i + 1, couponId: i + 1, orderId: 99));
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(existingCoupons);

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 0m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

            result.Should().Be(CouponApplyResult.OrderAlreadyHasCoupon);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponWithNoDiscount_ShouldReturnNoDiscountProduced()
        {
            var coupon = CreateAvailableCoupon(id: 5, code: "SAVE10");
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(new List<CouponUsed>());

            var result = await CreateService().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

            result.Should().Be(CouponApplyResult.NoDiscountProduced);
            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Never);
            _couponUsed.Verify(r => r.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
            _applicationRecords.Verify(r => r.AddAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        // ── RemoveCouponAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task RemoveCouponAsync_NoCouponApplied_ShouldReturnNoCouponApplied()
        {
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CouponUsed)null);

            var result = await CreateService().RemoveCouponAsync(99, TestContext.Current.CancellationToken);

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

            var result = await CreateService().RemoveCouponAsync(99, TestContext.Current.CancellationToken);

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
