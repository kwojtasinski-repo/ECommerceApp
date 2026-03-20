using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Contracts;
using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Domain.Sales.Fulfillment;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class ShipmentServiceTests
    {
        private readonly Mock<IShipmentRepository> _shipments;
        private readonly Mock<IOrderExistenceChecker> _orderExistence;
        private readonly Mock<IMessageBroker> _broker;

        public ShipmentServiceTests()
        {
            _shipments = new Mock<IShipmentRepository>();
            _orderExistence = new Mock<IOrderExistenceChecker>();
            _broker = new Mock<IMessageBroker>();
        }

        private IShipmentService CreateService()
            => new ShipmentService(_shipments.Object, _orderExistence.Object, _broker.Object);

        private static Shipment CreateShipment(
            int id = 1,
            int orderId = 99,
            ShipmentStatus status = ShipmentStatus.Pending)
        {
            var lines = new[] { ShipmentLine.Create(10, 2), ShipmentLine.Create(20, 1) };
            var shipment = Shipment.Create(orderId, lines);
            typeof(Shipment).GetProperty(nameof(Shipment.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(shipment, new object[] { new ShipmentId(id) });

            if (status == ShipmentStatus.InTransit)
                shipment.MarkAsInTransit("TRACK-001");
            else if (status == ShipmentStatus.Delivered)
            {
                shipment.MarkAsInTransit("TRACK-001");
                shipment.MarkAsDelivered();
            }
            else if (status == ShipmentStatus.Failed)
                shipment.MarkAsFailed();

            return shipment;
        }

        private static CreateShipmentDto CreateDto(
            int orderId = 99,
            IReadOnlyList<CreateShipmentLineDto>? lines = null)
            => new(orderId, lines ?? new List<CreateShipmentLineDto>
            {
                new(10, 2),
                new(20, 1)
            });

        // ── CreateShipmentAsync ───────────────────────────────────────────────

        [Fact]
        public async Task CreateShipmentAsync_OrderNotFound_ShouldReturnOrderNotFound()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await CreateService().CreateShipmentAsync(CreateDto());

            result.Should().Be(ShipmentOperationResult.OrderNotFound);
            _shipments.Verify(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateShipmentAsync_ValidRequest_WithSingleLine_ShouldReturnSuccess()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await CreateService().CreateShipmentAsync(CreateDto(
                lines: new List<CreateShipmentLineDto> { new(10, 1) }));

            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task CreateShipmentAsync_ValidRequest_ShouldPersistShipmentWithCorrectData()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await CreateService().CreateShipmentAsync(CreateDto());

            result.Should().Be(ShipmentOperationResult.Success);
            _shipments.Verify(r => r.AddAsync(
                It.Is<Shipment>(s =>
                    s.OrderId == 99 &&
                    s.Status == ShipmentStatus.Pending &&
                    s.Lines.Count == 2 &&
                    s.Lines[0].ProductId == 10 && s.Lines[0].Quantity == 2 &&
                    s.Lines[1].ProductId == 20 && s.Lines[1].Quantity == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateShipmentAsync_ValidRequest_ShouldNotPublishAnyMessage()
        {
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            await CreateService().CreateShipmentAsync(CreateDto());

            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        // ── MarkAsInTransitAsync ──────────────────────────────────────────────

        [Fact]
        public async Task MarkAsInTransitAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Shipment?)null);

            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-XYZ");

            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_PendingShipment_ShouldReturnSuccess()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-XYZ");

            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_PendingShipment_ShouldUpdateAndPublishShipmentDispatched()
        {
            var shipment = CreateShipment(id: 5, orderId: 99, status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsInTransitAsync(5, "TRACK-XYZ");

            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.InTransit);
            shipment.TrackingNumber.Should().Be("TRACK-XYZ");
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(ShipmentDispatched) &&
                ((ShipmentDispatched)m[0]).ShipmentId == 5 &&
                ((ShipmentDispatched)m[0]).OrderId == 99 &&
                ((ShipmentDispatched)m[0]).TrackingNumber == "TRACK-XYZ")), Times.Once);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyInTransit_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW");

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyDelivered_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW");

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyFailed_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW");

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        // ── MarkAsDeliveredAsync

        [Fact]
        public async Task MarkAsDeliveredAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Shipment?)null);

            var result = await CreateService().MarkAsDeliveredAsync(1);

            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_InTransitShipment_ShouldReturnSuccess()
        {
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsDeliveredAsync(1);

            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_InTransitShipment_ShouldUpdateAndPublishShipmentDelivered()
        {
            var shipment = CreateShipment(id: 7, orderId: 99, status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsDeliveredAsync(7);

            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Delivered);
            shipment.DeliveredAt.Should().NotBeNull();
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(ShipmentDelivered) &&
                ((ShipmentDelivered)m[0]).ShipmentId == 7 &&
                ((ShipmentDelivered)m[0]).OrderId == 99)), Times.Once);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_PendingShipment_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsDeliveredAsync(1);

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_AlreadyDelivered_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsDeliveredAsync(1);

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_FailedShipment_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsDeliveredAsync(1);

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        // ── MarkAsFailedAsync

        [Fact]
        public async Task MarkAsFailedAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Shipment?)null);

            var result = await CreateService().MarkAsFailedAsync(1);

            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsFailedAsync_PendingShipment_ShouldReturnSuccess()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(1);

            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsFailedAsync_PendingShipment_ShouldUpdateAndPublishShipmentFailed()
        {
            var shipment = CreateShipment(id: 3, orderId: 99, status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(3);

            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Failed);
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(ShipmentFailed) &&
                ((ShipmentFailed)m[0]).ShipmentId == 3 &&
                ((ShipmentFailed)m[0]).OrderId == 99)), Times.Once);
        }

        [Fact]
        public async Task MarkAsFailedAsync_InTransitShipment_ShouldReturnSuccess()
        {
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(1);

            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsFailedAsync_InTransitShipment_ShouldUpdateAndPublishShipmentFailed()
        {
            var shipment = CreateShipment(id: 4, orderId: 99, status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(4);

            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Failed);
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 1 &&
                m[0].GetType() == typeof(ShipmentFailed) &&
                ((ShipmentFailed)m[0]).ShipmentId == 4 &&
                ((ShipmentFailed)m[0]).OrderId == 99)), Times.Once);
        }

        [Fact]
        public async Task MarkAsFailedAsync_DeliveredShipment_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(1);

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsFailedAsync_AlreadyFailed_ShouldReturnInvalidStatus()
        {
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var result = await CreateService().MarkAsFailedAsync(1);

            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        // ── GetShipmentAsync

        [Fact]
        public async Task GetShipmentAsync_NotFound_ShouldReturnNull()
        {
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Shipment?)null);

            var result = await CreateService().GetShipmentAsync(1);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_PendingShipment_ShouldMapAllScalarFields()
        {
            var shipment = CreateShipment(id: 5, orderId: 99, status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var vm = await CreateService().GetShipmentAsync(5);

            vm.Should().NotBeNull();
            vm!.Id.Should().Be(5);
            vm.OrderId.Should().Be(99);
            vm.Status.Should().Be("Pending");
            vm.TrackingNumber.Should().BeNull();
            vm.ShippedAt.Should().BeNull();
            vm.DeliveredAt.Should().BeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_InTransitShipment_ShouldMapStatusAndTrackingNumber()
        {
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.InTransit);
            _shipments.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var vm = await CreateService().GetShipmentAsync(5);

            vm!.Status.Should().Be("InTransit");
            vm.TrackingNumber.Should().Be("TRACK-001");
            vm.ShippedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_DeliveredShipment_ShouldMapStatusAndDeliveredAt()
        {
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.Delivered);
            _shipments.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var vm = await CreateService().GetShipmentAsync(5);

            vm!.Status.Should().Be("Delivered");
            vm.TrackingNumber.Should().Be("TRACK-001");
            vm.ShippedAt.Should().NotBeNull();
            vm.DeliveredAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_FailedShipment_ShouldMapStatus()
        {
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.Failed);
            _shipments.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var vm = await CreateService().GetShipmentAsync(5);

            vm!.Status.Should().Be("Failed");
            vm.TrackingNumber.Should().BeNull();
            vm.DeliveredAt.Should().BeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_WithLines_ShouldMapAllLines()
        {
            var lines = new[]
            {
                ShipmentLine.Create(10, 2),
                ShipmentLine.Create(20, 1),
                ShipmentLine.Create(30, 3)
            };
            var shipment = Shipment.Create(99, lines);
            typeof(Shipment).GetProperty(nameof(Shipment.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(shipment, new object[] { new ShipmentId(1) });
            _shipments.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            var vm = await CreateService().GetShipmentAsync(1);

            vm!.Lines.Should().HaveCount(3);
            vm.Lines.Should().Contain(l => l.ProductId == 10 && l.Quantity == 2);
            vm.Lines.Should().Contain(l => l.ProductId == 20 && l.Quantity == 1);
            vm.Lines.Should().Contain(l => l.ProductId == 30 && l.Quantity == 3);
        }

        // ── GetShipmentsByOrderIdAsync ────────────────────────────────────────

        [Fact]
        public async Task GetShipmentsByOrderIdAsync_NoShipments_ShouldReturnEmptyList()
        {
            _shipments.Setup(x => x.GetByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Shipment>());

            var result = await CreateService().GetShipmentsByOrderIdAsync(99);

            result.Shipments.Should().BeEmpty();
        }

        [Fact]
        public async Task GetShipmentsByOrderIdAsync_MultipleShipments_ShouldReturnAll()
        {
            var s1 = CreateShipment(id: 1, orderId: 99, status: ShipmentStatus.Delivered);
            var s2 = CreateShipment(id: 2, orderId: 99, status: ShipmentStatus.Pending);
            _shipments.Setup(x => x.GetByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Shipment> { s1, s2 });

            var result = await CreateService().GetShipmentsByOrderIdAsync(99);

            result.Shipments.Should().HaveCount(2);
            result.Shipments.Should().Contain(s => s.Id == 1 && s.Status == "Delivered");
            result.Shipments.Should().Contain(s => s.Id == 2 && s.Status == "Pending");
        }
    }
}
