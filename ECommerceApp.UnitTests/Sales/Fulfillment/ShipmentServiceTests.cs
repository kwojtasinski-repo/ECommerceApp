using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment;
using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Domain.Sales.Fulfillment;
using AwesomeAssertions;
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
        private readonly Mock<IModuleClient> _moduleClient;
        private readonly Mock<IFulfillmentUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;

        public ShipmentServiceTests()
        {
            _shipments = new Mock<IShipmentRepository>();
            _moduleClient = new Mock<IModuleClient>();
            _unitOfWork = new Mock<IFulfillmentUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
        }

        private IShipmentService CreateService(Mock<IOutboxTransaction> txMock = null)
        {
            txMock ??= new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            return new ShipmentService(_shipments.Object, _moduleClient.Object, _unitOfWork.Object, _outboxWriter.Object);
        }

        private void SetupShipmentExists(int shipmentId, Shipment shipment)
            => _shipments.Setup(x => x.GetByIdAsync(shipmentId, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

        private void SetupShipmentMissing(int shipmentId)
            => _shipments.Setup(x => x.GetByIdAsync(shipmentId, It.IsAny<CancellationToken>())).ReturnsAsync((Shipment)null);

        private void SetupOrderExists(bool exists)
            => _moduleClient.Setup(x => x.SendAsync(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(exists);

        private void SetupShipmentsByOrder(int orderId, IReadOnlyList<Shipment> shipments)
            => _shipments.Setup(x => x.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(shipments);

        private static Shipment CreateShipment(
            int id = 1,
            int orderId = 99,
            ShipmentStatus status = ShipmentStatus.Pending)
        {
            var lines = new[] { ShipmentLine.Create(10, 2), ShipmentLine.Create(20, 1) };
            var shipment = Shipment.Create(orderId, lines);
            EntityIdSetter.Set(shipment, new ShipmentId(id));

            if (status == ShipmentStatus.InTransit)
            {
                shipment.MarkAsInTransit("TRACK-001");
            }
            else if (status == ShipmentStatus.Delivered)
            {
                shipment.MarkAsInTransit("TRACK-001");
                shipment.MarkAsDelivered();
            }
            else if (status == ShipmentStatus.Failed)
            {
                shipment.MarkAsFailed();
            }

            return shipment;
        }

        private static CreateShipmentDto CreateDto(
            int orderId = 99,
            IReadOnlyList<CreateShipmentLineDto> lines = null)
            => new(orderId, lines ??
            [
                new(10, 2),
                new(20, 1)
            ]);

        // ── CreateShipmentAsync ───────────────────────────────────────────────

        [Fact]
        public async Task CreateShipmentAsync_OrderNotFound_ShouldReturnOrderNotFound()
        {
            // Arrange
            SetupOrderExists(false);

            // Act
            var result = await CreateService().CreateShipmentAsync(CreateDto(), TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.OrderNotFound);
            _shipments.Verify(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateShipmentAsync_ValidRequest_WithSingleLine_ShouldReturnSuccess()
        {
            // Arrange
            SetupOrderExists(true);

            // Act
            var result = await CreateService().CreateShipmentAsync(CreateDto(
                lines: new List<CreateShipmentLineDto> { new(10, 1) }), TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task CreateShipmentAsync_ValidRequest_ShouldPersistShipmentWithCorrectData()
        {
            // Arrange
            SetupOrderExists(true);

            // Act
            var result = await CreateService().CreateShipmentAsync(CreateDto(), TestContext.Current.CancellationToken);

            // Assert
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
            // Arrange
            SetupOrderExists(true);

            // Act
            await CreateService().CreateShipmentAsync(CreateDto(), TestContext.Current.CancellationToken);

            // Assert
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsInTransitAsync ──────────────────────────────────────────────

        [Fact]
        public async Task MarkAsInTransitAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            SetupShipmentMissing(1);

            // Act
            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-XYZ", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_PendingShipment_ShouldReturnSuccess()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-XYZ", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_PendingShipment_ShouldUpdateAndPublishShipmentDispatched()
        {
            // Arrange
            var shipment = CreateShipment(id: 5, orderId: 99, status: ShipmentStatus.Pending);
            SetupShipmentExists(5, shipment);

            var txMock = new Mock<IOutboxTransaction>();
            // Act
            var result = await CreateService(txMock).MarkAsInTransitAsync(5, "TRACK-XYZ", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.InTransit);
            shipment.TrackingNumber.Should().Be("TRACK-XYZ");
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<ShipmentDispatched>(m =>
                    m.ShipmentId == 5 &&
                    m.OrderId == 99 &&
                    m.TrackingNumber == "TRACK-XYZ"),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyInTransit_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyDelivered_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsInTransitAsync_AlreadyFailed_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsInTransitAsync(1, "TRACK-NEW", TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsDeliveredAsync

        [Fact]
        public async Task MarkAsDeliveredAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            SetupShipmentMissing(1);

            // Act
            var result = await CreateService().MarkAsDeliveredAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_InTransitShipment_ShouldReturnSuccess()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsDeliveredAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_InTransitShipment_ShouldUpdateAndPublishShipmentDelivered()
        {
            // Arrange
            var shipment = CreateShipment(id: 7, orderId: 99, status: ShipmentStatus.InTransit);
            SetupShipmentExists(7, shipment);

            var txMock = new Mock<IOutboxTransaction>();
            // Act
            var result = await CreateService(txMock).MarkAsDeliveredAsync(7, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Delivered);
            shipment.DeliveredAt.Should().NotBeNull();
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<ShipmentDelivered>(m =>
                    m.ShipmentId == 7 &&
                    m.OrderId == 99),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_PendingShipment_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsDeliveredAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_AlreadyDelivered_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsDeliveredAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_FailedShipment_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsDeliveredAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsFailedAsync

        [Fact]
        public async Task MarkAsFailedAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            SetupShipmentMissing(1);

            // Act
            var result = await CreateService().MarkAsFailedAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsFailedAsync_PendingShipment_ShouldReturnSuccess()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsFailedAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsFailedAsync_PendingShipment_ShouldUpdateAndPublishShipmentFailed()
        {
            // Arrange
            var shipment = CreateShipment(id: 3, orderId: 99, status: ShipmentStatus.Pending);
            SetupShipmentExists(3, shipment);

            var txMock = new Mock<IOutboxTransaction>();
            // Act
            var result = await CreateService(txMock).MarkAsFailedAsync(3, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Failed);
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<ShipmentFailed>(m =>
                    m.ShipmentId == 3 &&
                    m.OrderId == 99),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsFailedAsync_InTransitShipment_ShouldReturnSuccess()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsFailedAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
        }

        [Fact]
        public async Task MarkAsFailedAsync_InTransitShipment_ShouldUpdateAndPublishShipmentFailed()
        {
            // Arrange
            var shipment = CreateShipment(id: 4, orderId: 99, status: ShipmentStatus.InTransit);
            SetupShipmentExists(4, shipment);

            var txMock = new Mock<IOutboxTransaction>();
            // Act
            var result = await CreateService(txMock).MarkAsFailedAsync(4, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.Failed);
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<ShipmentFailed>(m =>
                    m.ShipmentId == 4 &&
                    m.OrderId == 99),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsFailedAsync_DeliveredShipment_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Delivered);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsFailedAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsFailedAsync_AlreadyFailed_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Failed);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsFailedAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsPartiallyDeliveredAsync ─────────────────────────────────────

        [Fact]
        public async Task MarkAsPartiallyDeliveredAsync_ShipmentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            SetupShipmentMissing(1);

            // Act
            var result = await CreateService().MarkAsPartiallyDeliveredAsync(1, new List<int> { 10 }, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.NotFound);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsPartiallyDeliveredAsync_PendingShipment_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.Pending);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsPartiallyDeliveredAsync(1, new List<int> { 10 }, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsPartiallyDeliveredAsync_InTransitShipment_ShouldUpdateAndPublishShipmentPartiallyDeliveredWithSplitItems()
        {
            // Arrange
            var shipment = CreateShipment(id: 7, orderId: 99, status: ShipmentStatus.InTransit);
            SetupShipmentExists(7, shipment);

            var txMock = new Mock<IOutboxTransaction>();
            // Act
            var result = await CreateService(txMock).MarkAsPartiallyDeliveredAsync(7, new List<int> { 10 }, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.Success);
            shipment.Status.Should().Be(ShipmentStatus.PartiallyDelivered);
            _shipments.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<ShipmentPartiallyDelivered>(m =>
                    m.ShipmentId == 7 &&
                    m.OrderId == 99 &&
                    m.DeliveredItems.Count == 1 && m.DeliveredItems[0].ProductId == 10 && m.DeliveredItems[0].Quantity == 2 &&
                    m.FailedItems.Count == 1 && m.FailedItems[0].ProductId == 20 && m.FailedItems[0].Quantity == 1),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsPartiallyDeliveredAsync_NoDeliveredProductIds_ShouldReturnInvalidStatus()
        {
            // Arrange
            var shipment = CreateShipment(status: ShipmentStatus.InTransit);
            SetupShipmentExists(1, shipment);

            // Act
            var result = await CreateService().MarkAsPartiallyDeliveredAsync(1, new List<int>(), TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ShipmentOperationResult.InvalidStatus);
            _shipments.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── GetShipmentAsync

        [Fact]
        public async Task GetShipmentAsync_NotFound_ShouldReturnNull()
        {
            // Arrange
            SetupShipmentMissing(1);

            // Act
            var result = await CreateService().GetShipmentAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_PendingShipment_ShouldMapAllScalarFields()
        {
            // Arrange
            var shipment = CreateShipment(id: 5, orderId: 99, status: ShipmentStatus.Pending);
            SetupShipmentExists(5, shipment);

            // Act
            var vm = await CreateService().GetShipmentAsync(5, TestContext.Current.CancellationToken);

            // Assert
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
            // Arrange
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.InTransit);
            SetupShipmentExists(5, shipment);

            // Act
            var vm = await CreateService().GetShipmentAsync(5, TestContext.Current.CancellationToken);

            // Assert
            vm!.Status.Should().Be("InTransit");
            vm.TrackingNumber.Should().Be("TRACK-001");
            vm.ShippedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_DeliveredShipment_ShouldMapStatusAndDeliveredAt()
        {
            // Arrange
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.Delivered);
            SetupShipmentExists(5, shipment);

            // Act
            var vm = await CreateService().GetShipmentAsync(5, TestContext.Current.CancellationToken);

            // Assert
            vm!.Status.Should().Be("Delivered");
            vm.TrackingNumber.Should().Be("TRACK-001");
            vm.ShippedAt.Should().NotBeNull();
            vm.DeliveredAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_FailedShipment_ShouldMapStatus()
        {
            // Arrange
            var shipment = CreateShipment(id: 5, status: ShipmentStatus.Failed);
            SetupShipmentExists(5, shipment);

            // Act
            var vm = await CreateService().GetShipmentAsync(5, TestContext.Current.CancellationToken);

            // Assert
            vm!.Status.Should().Be("Failed");
            vm.TrackingNumber.Should().BeNull();
            vm.DeliveredAt.Should().BeNull();
        }

        [Fact]
        public async Task GetShipmentAsync_WithLines_ShouldMapAllLines()
        {
            // Arrange
            var lines = new[]
            {
                ShipmentLine.Create(10, 2),
                ShipmentLine.Create(20, 1),
                ShipmentLine.Create(30, 3)
            };
            var shipment = Shipment.Create(99, lines);
            EntityIdSetter.Set(shipment, new ShipmentId(1));
            SetupShipmentExists(1, shipment);

            // Act
            var vm = await CreateService().GetShipmentAsync(1, TestContext.Current.CancellationToken);

            // Assert
            vm!.Lines.Should().HaveCount(3);
            vm.Lines.Should().Contain(l => l.ProductId == 10 && l.Quantity == 2);
            vm.Lines.Should().Contain(l => l.ProductId == 20 && l.Quantity == 1);
            vm.Lines.Should().Contain(l => l.ProductId == 30 && l.Quantity == 3);
        }

        // ── GetShipmentsByOrderIdAsync ────────────────────────────────────────

        [Fact]
        public async Task GetShipmentsByOrderIdAsync_NoShipments_ShouldReturnEmptyList()
        {
            // Arrange
            SetupShipmentsByOrder(99, new List<Shipment>());

            // Act
            var result = await CreateService().GetShipmentsByOrderIdAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Shipments.Should().BeEmpty();
        }

        [Fact]
        public async Task GetShipmentsByOrderIdAsync_MultipleShipments_ShouldReturnAll()
        {
            // Arrange
            var s1 = CreateShipment(id: 1, orderId: 99, status: ShipmentStatus.Delivered);
            var s2 = CreateShipment(id: 2, orderId: 99, status: ShipmentStatus.Pending);
            SetupShipmentsByOrder(99, new List<Shipment> { s1, s2 });

            // Act
            var result = await CreateService().GetShipmentsByOrderIdAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Shipments.Should().HaveCount(2);
            result.Shipments.Should().Contain(s => s.Id == 1 && s.Status == "Delivered");
            result.Shipments.Should().Contain(s => s.Id == 2 && s.Status == "Pending");
        }
    }
}
