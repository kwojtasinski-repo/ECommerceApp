using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class ShipmentAggregateTests
    {
        private static ShipmentLine[] DefaultLines()
            => new[] { ShipmentLine.Create(10, 2) };

        private static Shipment CreatePending(int orderId = 1)
            => Shipment.Create(orderId, DefaultLines());

        private static Shipment CreateInTransit(int orderId = 1)
        {
            var s = CreatePending(orderId);
            s.MarkAsInTransit("TRACK-001");
            return s;
        }

        // ── Shipment.Create ───────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnPendingShipment()
        {
            var lines = new[] { ShipmentLine.Create(10, 3), ShipmentLine.Create(20, 1) };

            var shipment = Shipment.Create(99, lines);

            shipment.OrderId.Should().Be(99);
            shipment.Status.Should().Be(ShipmentStatus.Pending);
            shipment.TrackingNumber.Should().BeNull();
            shipment.ShippedAt.Should().BeNull();
            shipment.DeliveredAt.Should().BeNull();
            shipment.Lines.Should().HaveCount(2);
            shipment.Lines[0].ProductId.Should().Be(10);
            shipment.Lines[0].Quantity.Should().Be(3);
            shipment.Lines[1].ProductId.Should().Be(20);
            shipment.Lines[1].Quantity.Should().Be(1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_InvalidOrderId_ShouldThrowDomainException(int orderId)
        {
            var act = () => Shipment.Create(orderId, DefaultLines());

            act.Should().Throw<DomainException>().WithMessage("*OrderId*positive*");
        }

        [Fact]
        public void Create_NullLines_ShouldThrowDomainException()
        {
            var act = () => Shipment.Create(1, null!);

            act.Should().Throw<DomainException>().WithMessage("*Lines*required*");
        }

        [Fact]
        public void Create_EmptyLines_ShouldThrowDomainException()
        {
            var act = () => Shipment.Create(1, Array.Empty<ShipmentLine>());

            act.Should().Throw<DomainException>().WithMessage("*At least one*");
        }

        [Fact]
        public void Create_MultipleLinesAllPersisted()
        {
            var lines = Enumerable.Range(1, 5).Select(i => ShipmentLine.Create(i, i));

            var shipment = Shipment.Create(1, lines);

            shipment.Lines.Should().HaveCount(5);
        }

        // ── Shipment.MarkAsInTransit ──────────────────────────────────────────

        [Fact]
        public void MarkAsInTransit_FromPending_ShouldSetInTransitFields()
        {
            var shipment = CreatePending();
            var before = DateTime.UtcNow.AddSeconds(-1);

            shipment.MarkAsInTransit("TRACK-XYZ");

            shipment.Status.Should().Be(ShipmentStatus.InTransit);
            shipment.TrackingNumber.Should().Be("TRACK-XYZ");
            shipment.ShippedAt.Should().NotBeNull();
            shipment.ShippedAt!.Value.Should().BeOnOrAfter(before);
            shipment.DeliveredAt.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MarkAsInTransit_NullEmptyOrWhitespaceTracking_ShouldThrowDomainException(string? tracking)
        {
            var shipment = CreatePending();

            var act = () => shipment.MarkAsInTransit(tracking);

            act.Should().Throw<DomainException>().WithMessage("*Tracking number*required*");
        }

        [Fact]
        public void MarkAsInTransit_AlreadyInTransit_ShouldThrowDomainException()
        {
            var shipment = CreateInTransit();

            var act = () => shipment.MarkAsInTransit("TRACK-002");

            act.Should().Throw<DomainException>().WithMessage("*not in Pending*");
        }

        [Fact]
        public void MarkAsInTransit_FromDelivered_ShouldThrowDomainException()
        {
            var shipment = CreateInTransit();
            shipment.MarkAsDelivered();

            var act = () => shipment.MarkAsInTransit("TRACK-002");

            act.Should().Throw<DomainException>().WithMessage("*not in Pending*");
        }

        [Fact]
        public void MarkAsInTransit_FromFailed_ShouldThrowDomainException()
        {
            var shipment = CreatePending();
            shipment.MarkAsFailed();

            var act = () => shipment.MarkAsInTransit("TRACK-002");

            act.Should().Throw<DomainException>().WithMessage("*not in Pending*");
        }

        // ── Shipment.MarkAsDelivered ──────────────────────────────────────────

        [Fact]
        public void MarkAsDelivered_FromInTransit_ShouldSetDeliveredFields()
        {
            var shipment = CreateInTransit();
            var before = DateTime.UtcNow.AddSeconds(-1);

            shipment.MarkAsDelivered();

            shipment.Status.Should().Be(ShipmentStatus.Delivered);
            shipment.DeliveredAt.Should().NotBeNull();
            shipment.DeliveredAt!.Value.Should().BeOnOrAfter(before);
        }

        [Fact]
        public void MarkAsDelivered_FromPending_ShouldThrowDomainException()
        {
            var shipment = CreatePending();

            var act = () => shipment.MarkAsDelivered();

            act.Should().Throw<DomainException>().WithMessage("*not in transit*");
        }

        [Fact]
        public void MarkAsDelivered_AlreadyDelivered_ShouldThrowDomainException()
        {
            var shipment = CreateInTransit();
            shipment.MarkAsDelivered();

            var act = () => shipment.MarkAsDelivered();

            act.Should().Throw<DomainException>().WithMessage("*not in transit*");
        }

        [Fact]
        public void MarkAsDelivered_FromFailed_ShouldThrowDomainException()
        {
            var shipment = CreatePending();
            shipment.MarkAsFailed();

            var act = () => shipment.MarkAsDelivered();

            act.Should().Throw<DomainException>().WithMessage("*not in transit*");
        }

        // ── Shipment.MarkAsFailed ─────────────────────────────────────────────

        [Fact]
        public void MarkAsFailed_FromPending_ShouldSetFailedStatus()
        {
            var shipment = CreatePending();

            shipment.MarkAsFailed();

            shipment.Status.Should().Be(ShipmentStatus.Failed);
        }

        [Fact]
        public void MarkAsFailed_FromInTransit_ShouldSetFailedStatus()
        {
            var shipment = CreateInTransit();

            shipment.MarkAsFailed();

            shipment.Status.Should().Be(ShipmentStatus.Failed);
        }

        [Fact]
        public void MarkAsFailed_FromDelivered_ShouldThrowDomainException()
        {
            var shipment = CreateInTransit();
            shipment.MarkAsDelivered();

            var act = () => shipment.MarkAsFailed();

            act.Should().Throw<DomainException>().WithMessage("*cannot fail*Delivered*");
        }

        [Fact]
        public void MarkAsFailed_AlreadyFailed_ShouldThrowDomainException()
        {
            var shipment = CreatePending();
            shipment.MarkAsFailed();

            var act = () => shipment.MarkAsFailed();

            act.Should().Throw<DomainException>().WithMessage("*cannot fail*Failed*");
        }

        // ── ShipmentLine.Create ───────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ShipmentLine_Create_InvalidProductId_ShouldThrowDomainException(int productId)
        {
            var act = () => ShipmentLine.Create(productId, 1);

            act.Should().Throw<DomainException>().WithMessage("*ProductId*positive*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ShipmentLine_Create_InvalidQuantity_ShouldThrowDomainException(int quantity)
        {
            var act = () => ShipmentLine.Create(1, quantity);

            act.Should().Throw<DomainException>().WithMessage("*Quantity*positive*");
        }

        [Fact]
        public void ShipmentLine_Create_ValidParameters_ShouldSetFields()
        {
            var line = ShipmentLine.Create(42, 5);

            line.ProductId.Should().Be(42);
            line.Quantity.Should().Be(5);
        }
    }
}
