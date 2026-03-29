using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponScopeTargetTests
    {
        // ── Create — valid ────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldInitializeAllFields()
        {
            var couponId = new CouponId(5);

            var target = CouponScopeTarget.Create(couponId, "per-product", targetId: 42, targetName: "Widget X");

            target.CouponId.Should().Be(couponId);
            target.ScopeType.Value.Should().Be("per-product");
            target.TargetId.Should().Be(42);
            target.TargetName.Should().Be("Widget X");
        }

        [Fact]
        public void Create_NullTargetName_ShouldDefaultToEmpty()
        {
            var target = CouponScopeTarget.Create(new CouponId(1), "per-product", targetId: 42, targetName: null);

            target.TargetName.Should().BeEmpty();
        }

        // ── Create — domain guards ───────────────────────────────────────────

        [Fact]
        public void Create_NullCouponId_ShouldThrowDomainException()
        {
            var act = () => CouponScopeTarget.Create(null, "per-product", targetId: 42, targetName: "X");

            act.Should().Throw<DomainException>().WithMessage("*CouponId*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_EmptyScopeType_ShouldThrowDomainException(string scopeType)
        {
            var act = () => CouponScopeTarget.Create(new CouponId(1), scopeType, targetId: 42, targetName: "X");

            act.Should().Throw<DomainException>().WithMessage("*ScopeType*required*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_NonPositiveTargetId_ShouldThrowDomainException(int badId)
        {
            var act = () => CouponScopeTarget.Create(new CouponId(1), "per-product", targetId: badId, targetName: "X");

            act.Should().Throw<DomainException>().WithMessage("*TargetId*positive*");
        }

        // ── UpdateTargetName ──────────────────────────────────────────────────

        [Fact]
        public void UpdateTargetName_ShouldUpdateDisplaySnapshot()
        {
            var target = CouponScopeTarget.Create(new CouponId(1), "per-product", targetId: 42, targetName: "Old Name");

            target.UpdateTargetName("New Name");

            target.TargetName.Should().Be("New Name");
        }

        [Fact]
        public void UpdateTargetName_NullName_ShouldSetToEmpty()
        {
            var target = CouponScopeTarget.Create(new CouponId(1), "per-product", targetId: 42, targetName: "Old");

            target.UpdateTargetName(null);

            target.TargetName.Should().BeEmpty();
        }

        // ── ScopeType values ──────────────────────────────────────────────────

        [Theory]
        [InlineData("per-product")]
        [InlineData("per-category")]
        [InlineData("per-tag")]
        public void Create_AllScopeTypes_ShouldSucceed(string scopeType)
        {
            var target = CouponScopeTarget.Create(new CouponId(1), scopeType, targetId: 1, targetName: "T");

            target.ScopeType.Value.Should().Be(scopeType);
        }

        // ── No FK to Catalog ──────────────────────────────────────────────────

        [Fact]
        public void TargetId_ShouldBePlainInt_NoCatalogDependency()
        {
            var target = CouponScopeTarget.Create(new CouponId(1), "per-product", targetId: 999, targetName: "T");

            target.TargetId.Should().BeOfType(typeof(int));
            target.TargetId.Should().Be(999);
        }
    }
}
