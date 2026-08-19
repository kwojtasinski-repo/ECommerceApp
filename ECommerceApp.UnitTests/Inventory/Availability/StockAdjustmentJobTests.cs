using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class StockAdjustmentJobTests
    {
        private readonly Mock<IStockItemRepository> _stockItemRepo;
        private readonly Mock<IPendingStockAdjustmentRepository> _pendingAdjustmentRepo;
        private readonly Mock<IInventoryUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;
        private readonly Mock<IStockAuditRepository> _auditRepo;

        public StockAdjustmentJobTests()
        {
            _stockItemRepo = new Mock<IStockItemRepository>();
            _pendingAdjustmentRepo = new Mock<IPendingStockAdjustmentRepository>();
            _unitOfWork = new Mock<IInventoryUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
            _auditRepo = new Mock<IStockAuditRepository>();

            SetupTransaction();
        }

        private StockAdjustmentJob CreateJob() => new(
            _stockItemRepo.Object,
            _pendingAdjustmentRepo.Object,
            _unitOfWork.Object,
            _outboxWriter.Object,
            _auditRepo.Object);

        private static JobExecutionContext ContextFor(string entityId) =>
            new(entityId, Guid.NewGuid().ToString());

        private void SetupTransaction()
        {
            var txMock = new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
        }

        private void SetupPendingAdjustment(PendingStockAdjustment adjustment)
        {
            _pendingAdjustmentRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(adjustment);
        }

        private void SetupStock(int productId, StockItem stock)
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
        }

        // ── ExecuteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_ValidAdjustment_ShouldAdjustStockAndPublishAvailabilityChanged()
        {
            // Arrange
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(8));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            var context = ContextFor("1");

            SetupPendingAdjustment(pending);
            SetupStock(1, stock);

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _stockItemRepo.Verify(r => r.UpdateAsync(stock, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 8),
                It.IsAny<IOutboxTransaction>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoPendingAdjustment_ShouldReportSuccessNoOp()
        {
            // Arrange
            SetupPendingAdjustment(null);
            var context = ContextFor("1");

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _stockItemRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<StockAvailabilityChanged>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_StockNotFound_ShouldReportFailure()
        {
            // Arrange
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(5));
            SetupPendingAdjustment(pending);
            SetupStock(1, null);
            var context = ContextFor("1");

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<StockAvailabilityChanged>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NewQuantityBelowReserved_ShouldReportFailure()
        {
            // Arrange
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(1));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);
            SetupPendingAdjustment(pending);
            SetupStock(1, stock);
            var context = ContextFor("1");

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<StockAvailabilityChanged>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NullEntityId_ShouldReportFailure()
        {
            // Arrange
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _pendingAdjustmentRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidEntityId_ShouldReportFailure()
        {
            // Arrange
            var context = ContextFor("not-a-number");

            // Act
            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _pendingAdjustmentRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
