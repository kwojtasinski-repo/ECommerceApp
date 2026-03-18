using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponApplicationRecordTests
    {
        // ── Create — valid ────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldInitializeAllFields()
        {
            var record = CouponApplicationRecord.Create(
                couponUsedId: 7,
                couponCode: "SAVE15",
                discountType: "percentage-off",
                discountValue: 15m,
                originalTotal: 200m,
                reduction: 30m);

            record.CouponUsedId.Should().Be(7);
            record.CouponCode.Should().Be("SAVE15");
            record.DiscountType.Should().Be("percentage-off");
            record.DiscountValue.Should().Be(15m);
            record.OriginalTotal.Should().Be(200m);
            record.Reduction.Should().Be(30m);
            record.AppliedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            record.WasReversed.Should().BeFalse();
            record.ReversedAt.Should().BeNull();
        }

        // ── Create — domain guards ───────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_InvalidCouponUsedId_ShouldThrowDomainException(int badId)
        {
            var act = () => CouponApplicationRecord.Create(badId, "CODE", "pct", 10m, 100m, 10m);

            act.Should().Throw<DomainException>().WithMessage("*CouponUsedId*positive*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_EmptyCouponCode_ShouldThrowDomainException(string code)
        {
            var act = () => CouponApplicationRecord.Create(1, code, "pct", 10m, 100m, 10m);

            act.Should().Throw<DomainException>().WithMessage("*CouponCode*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_EmptyDiscountType_ShouldThrowDomainException(string type)
        {
            var act = () => CouponApplicationRecord.Create(1, "CODE", type, 10m, 100m, 10m);

            act.Should().Throw<DomainException>().WithMessage("*DiscountType*required*");
        }

        [Fact]
        public void Create_NegativeReduction_ShouldThrowDomainException()
        {
            var act = () => CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, -5m);

            act.Should().Throw<DomainException>().WithMessage("*Reduction*negative*");
        }

        // ── MarkAsReversed ────────────────────────────────────────────────────

        [Fact]
        public void MarkAsReversed_NotYetReversed_ShouldSetWasReversedAndTimestamp()
        {
            var record = CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m);

            record.MarkAsReversed();

            record.WasReversed.Should().BeTrue();
            record.ReversedAt.Should().NotBeNull();
            record.ReversedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void MarkAsReversed_AlreadyReversed_ShouldThrowDomainException()
        {
            var record = CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m);
            record.MarkAsReversed();

            var act = () => record.MarkAsReversed();

            act.Should().Throw<DomainException>().WithMessage("*already reversed*");
        }

        // ── Immutability ──────────────────────────────────────────────────────

        [Fact]
        public void Create_Record_ShouldNeverBeDeletedOnlyMarkedReversed()
        {
            var record = CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m);
            record.MarkAsReversed();

            record.WasReversed.Should().BeTrue();
            record.CouponUsedId.Should().Be(1);
            record.CouponCode.Should().Be("CODE");
        }

        // ── CouponUsedId is plain int — survives CouponUsed deletion ──────────

        [Fact]
        public void CouponUsedId_ShouldBePlainInt_NotTypedId()
        {
            var record = CouponApplicationRecord.Create(42, "CODE", "pct", 10m, 100m, 10m);

            record.CouponUsedId.Should().BeOfType(typeof(int));
            record.CouponUsedId.Should().Be(42);
        }
    }
}
