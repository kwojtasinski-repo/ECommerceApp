using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Messaging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class InventoryRefundApprovedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly Mock<IInventoryUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxTransaction> _transaction = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly RefundApprovedHandler _handler;

        public InventoryRefundApprovedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transaction.Object);
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(
                It.IsAny<long>(), It.IsAny<string>(), _transaction.Object, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _handler = new RefundApprovedHandler(
                _stockService.Object,
                _unitOfWork.Object,
                _processedMessageGuard.Object,
                _outboxWriter.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldCallReturnForEachItem()
        {
            var message = new RefundApproved(
                RefundId: 1,
                OrderId: 1,
                Items: new List<RefundApprovedItem>
                {
                    new RefundApprovedItem(ProductId: 42, Quantity: 3),
                    new RefundApprovedItem(ProductId: 10, Quantity: 1)
                },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReturnAsync(42, 3, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.Verify(s => s.ReturnAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ShouldNotCallOtherStockMethods()
        {
            var message = new RefundApproved(
                RefundId: 2,
                OrderId: 5,
                Items: new List<RefundApprovedItem>
                {
                    new RefundApprovedItem(ProductId: 10, Quantity: 1)
                },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReturnAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldPublishStockReturnedAfterReturningStock()
        {
            var message = new RefundApproved(
                RefundId: 8,
                OrderId: 1,
                Items: new List<RefundApprovedItem>(),
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<RefundStockReturned>(m => m.RefundId == 8),
                _transaction.Object,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
