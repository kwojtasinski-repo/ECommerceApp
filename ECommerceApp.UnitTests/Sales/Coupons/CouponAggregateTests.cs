using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponAggregateTests
    {
        private static Coupon CreateAvailable() => Coupon.Create("SAVE10", "10% off everything");

        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnAvailableCoupon()
        {
            var coupon = Coupon.Create("SAVE10", "10% off everything");

            coupon.Code.Value.Should().Be("SAVE10");
            coupon.Description.Value.Should().Be("10% off everything");
            coupon.Status.Should().Be(CouponStatus.Available);
        }

        // ── MarkAsUsed ────────────────────────────────────────────────────────

        [Fact]
        public void MarkAsUsed_AvailableCoupon_ShouldSetStatusToUsed()
        {
            var coupon = CreateAvailable();

            coupon.MarkAsUsed();

            coupon.Status.Should().Be(CouponStatus.Used);
        }

        [Fact]
        public void MarkAsUsed_AlreadyUsedCoupon_ShouldThrowDomainException()
        {
            var coupon = CreateAvailable();
            coupon.MarkAsUsed();

            var act = () => coupon.MarkAsUsed();

            act.Should().Throw<DomainException>().WithMessage("*SAVE10*not available*");
        }

        // ── Release ───────────────────────────────────────────────────────────

        [Fact]
        public void Release_UsedCoupon_ShouldSetStatusToAvailable()
        {
            var coupon = CreateAvailable();
            coupon.MarkAsUsed();

            coupon.Release();

            coupon.Status.Should().Be(CouponStatus.Available);
        }

        [Fact]
        public void Release_AvailableCoupon_ShouldThrowDomainException()
        {
            var coupon = CreateAvailable();

            var act = () => coupon.Release();

            act.Should().Throw<DomainException>().WithMessage("*SAVE10*not in Used status*");
        }
    }
}
