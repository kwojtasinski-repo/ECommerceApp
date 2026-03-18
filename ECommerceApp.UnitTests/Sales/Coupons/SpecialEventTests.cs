using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class SpecialEventTests
    {
        // ── Create — valid ────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldInitializeActiveEvent()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);

            var evt = SpecialEvent.Create("BLACK_FRIDAY", "Black Friday 2026", start, end);

            evt.Code.Should().Be("BLACK_FRIDAY");
            evt.Name.Should().Be("Black Friday 2026");
            evt.StartsAt.Should().Be(start);
            evt.EndsAt.Should().Be(end);
            evt.IsActive.Should().BeTrue();
        }

        // ── Create — domain guards ───────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_EmptyCode_ShouldThrowDomainException(string code)
        {
            var act = () => SpecialEvent.Create(code, "Name",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

            act.Should().Throw<DomainException>().WithMessage("*code*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_EmptyName_ShouldThrowDomainException(string name)
        {
            var act = () => SpecialEvent.Create("CODE", name,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

            act.Should().Throw<DomainException>().WithMessage("*name*required*");
        }

        [Fact]
        public void Create_EndsAtBeforeStartsAt_ShouldThrowDomainException()
        {
            var start = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);

            var act = () => SpecialEvent.Create("CODE", "Name", start, end);

            act.Should().Throw<DomainException>().WithMessage("*EndsAt*after*StartsAt*");
        }

        [Fact]
        public void Create_EndsAtEqualsStartsAt_ShouldThrowDomainException()
        {
            var date = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            var act = () => SpecialEvent.Create("CODE", "Name", date, date);

            act.Should().Throw<DomainException>().WithMessage("*EndsAt*after*StartsAt*");
        }

        // ── IsCurrentlyActive ─────────────────────────────────────────────────

        [Fact]
        public void IsCurrentlyActive_WithinRange_IsActive_ShouldReturnTrue()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);
            var now = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

            evt.IsCurrentlyActive(now).Should().BeTrue();
        }

        [Fact]
        public void IsCurrentlyActive_ExactlyAtStartsAt_ShouldReturnTrue()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);

            evt.IsCurrentlyActive(start).Should().BeTrue();
        }

        [Fact]
        public void IsCurrentlyActive_ExactlyAtEndsAt_ShouldReturnTrue()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);

            evt.IsCurrentlyActive(end).Should().BeTrue();
        }

        [Fact]
        public void IsCurrentlyActive_BeforeRange_ShouldReturnFalse()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);
            var now = new DateTime(2026, 11, 24, 23, 59, 59, DateTimeKind.Utc);

            evt.IsCurrentlyActive(now).Should().BeFalse();
        }

        [Fact]
        public void IsCurrentlyActive_AfterRange_ShouldReturnFalse()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);
            var now = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            evt.IsCurrentlyActive(now).Should().BeFalse();
        }

        [Fact]
        public void IsCurrentlyActive_Deactivated_WithinRange_ShouldReturnFalse()
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
            var evt = SpecialEvent.Create("BF", "Black Friday", start, end);
            evt.Deactivate();
            var now = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

            evt.IsCurrentlyActive(now).Should().BeFalse();
        }

        // ── Deactivate / Activate ─────────────────────────────────────────────

        [Fact]
        public void Deactivate_ShouldSetIsActiveFalse()
        {
            var evt = SpecialEvent.Create("BF", "Black Friday",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(5));

            evt.Deactivate();

            evt.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Activate_AfterDeactivation_ShouldSetIsActiveTrue()
        {
            var evt = SpecialEvent.Create("BF", "Black Friday",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(5));
            evt.Deactivate();

            evt.Activate();

            evt.IsActive.Should().BeTrue();
        }
    }
}
