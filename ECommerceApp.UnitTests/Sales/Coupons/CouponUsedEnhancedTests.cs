using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponUsedEnhancedTests
    {
        // ── CreateForDbCoupon ─────────────────────────────────────────────────

        [Fact]
        public void CreateForDbCoupon_ValidParameters_ShouldSetCouponIdAndUserId()
        {
            var couponId = new CouponId(5);

            var cu = CouponUsed.CreateForDbCoupon(couponId, orderId: 99, userId: "user-1");

            cu.CouponId.Should().Be(couponId);
            cu.OrderId.Should().Be(99);
            cu.UserId.Should().Be("user-1");
            cu.RuntimeCouponSnapshot.Should().BeNull();
            cu.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void CreateForDbCoupon_NullCouponId_ShouldThrowDomainException()
        {
            var act = () => CouponUsed.CreateForDbCoupon(null, orderId: 99, userId: "user-1");

            act.Should().Throw<DomainException>().WithMessage("*CouponId*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateForDbCoupon_EmptyUserId_ShouldThrowDomainException(string userId)
        {
            var act = () => CouponUsed.CreateForDbCoupon(new CouponId(1), orderId: 99, userId: userId);

            act.Should().Throw<DomainException>().WithMessage("*UserId*required*");
        }

        // ── CreateForRuntimeCoupon ────────────────────────────────────────────

        [Fact]
        public void CreateForRuntimeCoupon_ValidParameters_ShouldSetSnapshotAndUserId()
        {
            var snapshot = "{\"code\":\"ML10\",\"source\":\"ml-engine\",\"discountPercent\":10,\"scope\":\"order-total\"}";

            var cu = CouponUsed.CreateForRuntimeCoupon(snapshot, orderId: 99, userId: "user-1");

            cu.RuntimeCouponSnapshot.Should().Be(snapshot);
            cu.OrderId.Should().Be(99);
            cu.UserId.Should().Be("user-1");
            cu.CouponId.Should().BeNull();
            cu.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateForRuntimeCoupon_EmptySnapshot_ShouldThrowDomainException(string snapshot)
        {
            var act = () => CouponUsed.CreateForRuntimeCoupon(snapshot, orderId: 99, userId: "user-1");

            act.Should().Throw<DomainException>().WithMessage("*RuntimeCouponSnapshot*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateForRuntimeCoupon_EmptyUserId_ShouldThrowDomainException(string userId)
        {
            var act = () => CouponUsed.CreateForRuntimeCoupon("{}", orderId: 99, userId: userId);

            act.Should().Throw<DomainException>().WithMessage("*UserId*required*");
        }

        // ── Invariant: exactly one of CouponId / RuntimeCouponSnapshot ────────

        [Fact]
        public void CreateForDbCoupon_ShouldHaveCouponIdSetAndSnapshotNull()
        {
            var cu = CouponUsed.CreateForDbCoupon(new CouponId(1), orderId: 1, userId: "u");

            cu.CouponId.Should().NotBeNull();
            cu.RuntimeCouponSnapshot.Should().BeNull();
        }

        [Fact]
        public void CreateForRuntimeCoupon_ShouldHaveSnapshotSetAndCouponIdNull()
        {
            var cu = CouponUsed.CreateForRuntimeCoupon("{\"x\":1}", orderId: 1, userId: "u");

            cu.CouponId.Should().BeNull();
            cu.RuntimeCouponSnapshot.Should().NotBeNull();
        }

        // ── Slice 1 backward compat ───────────────────────────────────────────

        [Fact]
        public void Create_Slice1Factory_ShouldStillWork()
        {
            var cu = CouponUsed.Create(new CouponId(5), orderId: 99);

            cu.CouponId.Should().Be(new CouponId(5));
            cu.OrderId.Should().Be(99);
            cu.UserId.Should().BeNull();
            cu.RuntimeCouponSnapshot.Should().BeNull();
        }
    }
}
