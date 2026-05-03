using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class PresaleDomainTests
    {
        // ── Quantity ──────────────────────────────────────────────────────────

        [Fact]
        public void Quantity_PositiveValue_ShouldBeCreated()
        {
            var q = new Quantity(5);

            q.Value.Should().Be(5);
        }

        [Fact]
        public void Quantity_ZeroValue_ShouldThrowDomainException()
        {
            var act = () => new Quantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Quantity_NegativeValue_ShouldThrowDomainException()
        {
            var act = () => new Quantity(-3);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        // ── CartLine ──────────────────────────────────────────────────────────

        [Fact]
        public void CartLine_Create_ValidParameters_ShouldCreateLine()
        {
            var line = CartLine.Create("user-1", 42, 3);

            line.UserId.Value.Should().Be("user-1");
            line.ProductId.Value.Should().Be(42);
            line.Quantity.Value.Should().Be(3);
        }

        [Fact]
        public void CartLine_UpdateQuantity_ValidQuantity_ShouldUpdate()
        {
            var line = CartLine.Create("user-1", 42, 3);

            line.UpdateQuantity(10);

            line.Quantity.Value.Should().Be(10);
        }

        [Fact]
        public void CartLine_UpdateQuantity_ZeroQuantity_ShouldThrowDomainException()
        {
            var line = CartLine.Create("user-1", 42, 3);

            var act = () => line.UpdateQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void CartLine_UpdateQuantity_NegativeQuantity_ShouldThrowDomainException()
        {
            var line = CartLine.Create("user-1", 42, 3);

            var act = () => line.UpdateQuantity(-1);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        // ── SoftReservation ───────────────────────────────────────────────────

        [Fact]
        public void SoftReservation_Create_ValidParameters_ShouldCreateReservation()
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(15);

            var reservation = SoftReservation.Create(1, "user-1", 2, 49.99m, expiresAt);

            reservation.ProductId.Value.Should().Be(1);
            reservation.UserId.Value.Should().Be("user-1");
            reservation.Quantity.Value.Should().Be(2);
            reservation.UnitPrice.Amount.Should().Be(49.99m);
            reservation.ExpiresAt.Should().Be(expiresAt);
        }

        [Fact]
        public void SoftReservation_Create_ZeroQuantity_ShouldThrowDomainException()
        {
            var act = () => SoftReservation.Create(1, "user-1", 0, 10m, DateTime.UtcNow.AddMinutes(15));

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void SoftReservation_Create_ZeroUnitPrice_ShouldThrowDomainException()
        {
            var act = () => SoftReservation.Create(1, "user-1", 1, 0m, DateTime.UtcNow.AddMinutes(15));

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void SoftReservation_Create_NegativeUnitPrice_ShouldThrowDomainException()
        {
            var act = () => SoftReservation.Create(1, "user-1", 1, -5m, DateTime.UtcNow.AddMinutes(15));

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void SoftReservation_Create_HasDefaultIdSentinel()
        {
            var reservation = SoftReservation.Create(1, "user-1", 1, 10m, DateTime.UtcNow.AddMinutes(15));

            reservation.Id.Should().Be(default);
        }

        // ── StockSnapshot ─────────────────────────────────────────────────────

        [Fact]
        public void StockSnapshot_Create_ValidParameters_ShouldCreateSnapshot()
        {
            var syncedAt = DateTime.UtcNow;

            var snapshot = StockSnapshot.Create(1, 100, syncedAt);

            snapshot.ProductId.Value.Should().Be(1);
            snapshot.AvailableQuantity.Should().Be(100);
            snapshot.LastSyncedAt.Should().Be(syncedAt);
        }

        [Fact]
        public void StockSnapshot_Update_ShouldChangeAvailableQuantityAndLastSyncedAt()
        {
            var snapshot = StockSnapshot.Create(1, 100, DateTime.UtcNow.AddMinutes(-5));
            var newSyncedAt = DateTime.UtcNow;

            snapshot.Update(80, newSyncedAt);

            snapshot.AvailableQuantity.Should().Be(80);
            snapshot.LastSyncedAt.Should().Be(newSyncedAt);
        }

        [Fact]
        public void StockSnapshot_Update_ToZero_ShouldBeAllowed()
        {
            var snapshot = StockSnapshot.Create(1, 5, DateTime.UtcNow);

            snapshot.Update(0, DateTime.UtcNow);

            snapshot.AvailableQuantity.Should().Be(0);
        }
    }
}
