using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class StockHoldAggregateTests
    {
        private static StockHold CreateGuaranteedHold()
            => StockHold.Create(
                new StockProductId(1),
                new ReservationOrderId(42),
                3,
                DateTime.UtcNow.AddHours(1));

        private static StockHold CreateConfirmedHold()
        {
            var hold = CreateGuaranteedHold();
            hold.Confirm();
            return hold;
        }

        // ── Confirm ────────────────────────────────────────────────────────────

        [Fact]
        public void Confirm_FromGuaranteed_ShouldTransitionToConfirmed()
        {
            var hold = CreateGuaranteedHold();
            hold.Confirm();
            hold.Status.Should().Be(StockHoldStatus.Confirmed);
        }

        [Fact]
        public void Confirm_FromReleased_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.MarkAsReleased();
            var act = () => hold.Confirm();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Confirm_FromFulfilled_ShouldThrowDomainException()
        {
            var hold = CreateConfirmedHold();
            hold.MarkAsFulfilled();
            var act = () => hold.Confirm();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Confirm_FromWithdrawn_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.Withdraw();
            var act = () => hold.Confirm();
            act.Should().Throw<DomainException>();
        }

        // ── MarkAsReleased ─────────────────────────────────────────────────────

        [Fact]
        public void MarkAsReleased_FromGuaranteed_ShouldTransitionToReleased()
        {
            var hold = CreateGuaranteedHold();
            hold.MarkAsReleased();
            hold.Status.Should().Be(StockHoldStatus.Released);
        }

        [Fact]
        public void MarkAsReleased_FromConfirmed_ShouldTransitionToReleased()
        {
            var hold = CreateConfirmedHold();
            hold.MarkAsReleased();
            hold.Status.Should().Be(StockHoldStatus.Released);
        }

        [Fact]
        public void MarkAsReleased_FromFulfilled_ShouldThrowDomainException()
        {
            var hold = CreateConfirmedHold();
            hold.MarkAsFulfilled();
            var act = () => hold.MarkAsReleased();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkAsReleased_FromWithdrawn_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.Withdraw();
            var act = () => hold.MarkAsReleased();
            act.Should().Throw<DomainException>();
        }

        // ── MarkAsFulfilled ────────────────────────────────────────────────────

        [Fact]
        public void MarkAsFulfilled_FromGuaranteed_ShouldTransitionToFulfilled()
        {
            var hold = CreateGuaranteedHold();
            hold.MarkAsFulfilled();
            hold.Status.Should().Be(StockHoldStatus.Fulfilled);
        }

        [Fact]
        public void MarkAsFulfilled_FromConfirmed_ShouldTransitionToFulfilled()
        {
            var hold = CreateConfirmedHold();
            hold.MarkAsFulfilled();
            hold.Status.Should().Be(StockHoldStatus.Fulfilled);
        }

        [Fact]
        public void MarkAsFulfilled_FromReleased_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.MarkAsReleased();
            var act = () => hold.MarkAsFulfilled();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkAsFulfilled_FromWithdrawn_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.Withdraw();
            var act = () => hold.MarkAsFulfilled();
            act.Should().Throw<DomainException>();
        }

        // ── Withdraw ────────────────────────────────────────────────────────────

        [Fact]
        public void Withdraw_FromGuaranteed_ShouldTransitionToWithdrawn()
        {
            var hold = CreateGuaranteedHold();
            hold.Withdraw();
            hold.Status.Should().Be(StockHoldStatus.Withdrawn);
        }

        [Fact]
        public void Withdraw_FromConfirmed_ShouldTransitionToWithdrawn()
        {
            var hold = CreateConfirmedHold();
            hold.Withdraw();
            hold.Status.Should().Be(StockHoldStatus.Withdrawn);
        }

        [Fact]
        public void Withdraw_FromReleased_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.MarkAsReleased();
            var act = () => hold.Withdraw();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Withdraw_FromFulfilled_ShouldThrowDomainException()
        {
            var hold = CreateConfirmedHold();
            hold.MarkAsFulfilled();
            var act = () => hold.Withdraw();
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Withdraw_WhenAlreadyWithdrawn_ShouldThrowDomainException()
        {
            var hold = CreateGuaranteedHold();
            hold.Withdraw();
            var act = () => hold.Withdraw();
            act.Should().Throw<DomainException>();
        }
    }
}
