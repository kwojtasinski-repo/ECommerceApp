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
        private readonly Mock<IMessageBroker> _broker;
        private readonly Mock<IStockAuditRepository> _auditRepo;

        public StockAdjustmentJobTests()
        {
            _stockItemRepo = new Mock<IStockItemRepository>();
            _pendingAdjustmentRepo = new Mock<IPendingStockAdjustmentRepository>();
            _broker = new Mock<IMessageBroker>();
            _auditRepo = new Mock<IStockAuditRepository>();
        }

        private StockAdjustmentJob CreateJob() => new(
            _stockItemRepo.Object,
            _pendingAdjustmentRepo.Object,
            _broker.Object,
            _auditRepo.Object);

        private static JobExecutionContext ContextFor(string entityId) =>
            new(entityId, Guid.NewGuid().ToString());

        // ── ExecuteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_ValidAdjustment_ShouldAdjustStockAndPublishAvailabilityChanged()
        {
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(8));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            var context = ContextFor("1");

            _pendingAdjustmentRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pending);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _stockItemRepo.Verify(r => r.UpdateAsync(stock, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 8)), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoPendingAdjustment_ShouldReportSuccessNoOp()
        {
            _pendingAdjustmentRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PendingStockAdjustment)null);
            var context = ContextFor("1");

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _stockItemRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<StockAvailabilityChanged>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_StockNotFound_ShouldReportFailure()
        {
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(5));
            _pendingAdjustmentRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pending);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);
            var context = ContextFor("1");

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _broker.Verify(b => b.PublishAsync(It.IsAny<StockAvailabilityChanged>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NewQuantityBelowReserved_ShouldReportFailure()
        {
            var pending = PendingStockAdjustment.Create(new StockProductId(1), new StockQuantity(1));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);
            _pendingAdjustmentRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pending);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
            var context = ContextFor("1");

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _broker.Verify(b => b.PublishAsync(It.IsAny<StockAvailabilityChanged>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NullEntityId_ShouldReportFailure()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _pendingAdjustmentRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidEntityId_ShouldReportFailure()
        {
            var context = ContextFor("not-a-number");

            await CreateJob().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _pendingAdjustmentRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
