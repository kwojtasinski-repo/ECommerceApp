using AwesomeAssertions;
using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
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

        public ShipmentDeliveredHandlerTests()
        {
            _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_txMock.Object);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private ShipmentDeliveredHandler CreateHandler()
            => new(_stockService.Object, _unitOfWork.Object, _outboxWriter.Object);

        private static ShipmentDelivered CreateMessage(int orderId, params ShipmentLineItem[] items)
            => new(ShipmentId: 100, OrderId: orderId, Items: items, OccurredAt: DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_AllItemsFulfillSuccessfully_ShouldNotEnqueueReconciliation()
        {
            _stockService.Setup(s => s.FulfillAsync(42, 1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var message = CreateMessage(42, new ShipmentLineItem(1, 2));

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SomeItemsFailToFulfill_ShouldEnqueueStockReconciliationRequiredAndCommit()
        {
            _stockService.Setup(s => s.FulfillAsync(42, 1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _stockService.Setup(s => s.FulfillAsync(42, 2, 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var message = CreateMessage(42, new ShipmentLineItem(1, 2), new ShipmentLineItem(2, 5));

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

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
    }
}
