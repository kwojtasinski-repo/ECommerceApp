using AwesomeAssertions;
using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.UnitTests.Shared.Setup;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability.Handlers
{
    public class ShipmentDeliveredHandlerTests
    {
        private readonly Mock<IStockService> _stockService = new();
        private readonly Mock<IInventoryUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly Mock<IOutboxTransaction> _txMock = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        public ShipmentDeliveredHandlerTests()
        {
            SetupShipmentProcessingDefaults();
        }

        private void SetupShipmentProcessingDefaults()
        {
            _unitOfWork.SetupInventoryTransaction(_txMock);
            _outboxWriter.SetupSuccessfulOutboxEnqueue();
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(
                It.IsAny<long>(), It.IsAny<string>(), _txMock.Object, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        private ShipmentDeliveredHandler CreateHandler()
            => new(_stockService.Object, _unitOfWork.Object, _outboxWriter.Object, _processedMessageGuard.Object);

        private static ShipmentDelivered CreateMessage(int orderId, params ShipmentLineItem[] items)
            => new(ShipmentId: 100, OrderId: orderId, Items: items, OccurredAt: DateTime.UtcNow);

        private void SetupFulfillment(int orderId, int productId, int quantity, bool succeeded)
        {
            _stockService.Setup(s => s.FulfillAsync(orderId, productId, quantity, It.IsAny<CancellationToken>()))
                .ReturnsAsync(succeeded);
        }

        private void SetupAlreadyProcessed()
        {
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(
                1, It.IsAny<string>(), _txMock.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        [Fact]
        public async Task HandleAsync_AllItemsFulfillSuccessfully_ShouldNotEnqueueReconciliation()
        {
            // Arrange
            SetupFulfillment(42, 1, 2, true);
            var message = CreateMessage(42, new ShipmentLineItem(1, 2));

            // Act
            await CreateHandler().HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SomeItemsFailToFulfill_ShouldEnqueueStockReconciliationRequiredAndCommit()
        {
            // Arrange
            SetupFulfillment(42, 1, 2, true);
            SetupFulfillment(42, 2, 5, false);
            var message = CreateMessage(42, new ShipmentLineItem(1, 2), new ShipmentLineItem(2, 5));

            // Act
            await CreateHandler().HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockReconciliationRequired>(m => m.OrderId == 42
                    && m.Failures.Count == 1
                    && m.Failures[0].ProductId == 2
                    && m.Failures[0].Quantity == 5
                    && m.Failures[0].OperationType == StockOperationType.Fulfill),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_AlreadyProcessed_ShouldSkipAndNotCommit()
        {
            // Arrange
            SetupAlreadyProcessed();
            var message = CreateMessage(42, new ShipmentLineItem(1, 2));

            // Act
            await CreateHandler().HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _stockService.Verify(s => s.FulfillAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
