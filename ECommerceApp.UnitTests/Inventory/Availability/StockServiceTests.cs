using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class StockServiceTests
    {
        private readonly Mock<IStockItemRepository> _stockItemRepo;
        private readonly Mock<IStockHoldRepository> _stockHoldRepo;
        private readonly Mock<IProductSnapshotRepository> _productSnapshotRepo;
        private readonly Mock<IPendingStockAdjustmentRepository> _pendingAdjustmentRepo;
        private readonly Mock<IDeferredJobScheduler> _deferredScheduler;
        private readonly Mock<IInventoryUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;
        private readonly Mock<IStockAuditRepository> _auditRepo;
        private readonly Mock<IOutboxTransaction> _txMock;

        public StockServiceTests()
        {
            _stockItemRepo = new Mock<IStockItemRepository>();
            _stockHoldRepo = new Mock<IStockHoldRepository>();
            _productSnapshotRepo = new Mock<IProductSnapshotRepository>();
            _pendingAdjustmentRepo = new Mock<IPendingStockAdjustmentRepository>();
            _deferredScheduler = new Mock<IDeferredJobScheduler>();
            _unitOfWork = new Mock<IInventoryUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
            _auditRepo = new Mock<IStockAuditRepository>();

            _txMock = new Mock<IOutboxTransaction>();
            SetupTransactionDefaults();
        }

        private StockService CreateService() => new(
            _stockItemRepo.Object,
            _stockHoldRepo.Object,
            _productSnapshotRepo.Object,
            _pendingAdjustmentRepo.Object,
            _deferredScheduler.Object,
            _unitOfWork.Object,
            _outboxWriter.Object,
            _auditRepo.Object);

        private StockItem SetupAvailableStock(int productId, int quantity)
        {
            var (stock, _) = StockItem.Create(new StockProductId(productId), new StockQuantity(quantity));
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
            return stock;
        }

        private void SetupTransactionDefaults()
        {
            _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_txMock.Object);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private StockHold SetupReservedStockHold(
            int orderId, int productId, int holdQuantity, int reservedQuantity)
        {
            var hold = StockHold.Create(new StockProductId(productId), new ReservationOrderId(orderId), holdQuantity, DateTime.UtcNow.AddHours(1));
            var stock = SetupAvailableStock(productId, 10);
            stock.Reserve(reservedQuantity);

            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(orderId, productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(hold);

            return hold;
        }

        private void SetupStockMissing(int productId)
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);
        }

        private void SetupProductSnapshot(int productId, ProductSnapshot snapshot)
        {
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
        }

        private void SetupProductSnapshotMissing(int productId)
        {
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductSnapshot)null);
        }

        private void SetupStockHoldMissing(int orderId, int productId)
        {
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(orderId, productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockHold)null);
        }

        private void SetupStockHold(int orderId, int productId, StockHold hold)
        {
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(orderId, productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(hold);
        }

        private void SetupHoldsByOrder(int orderId, params StockHold[] holds)
        {
            _stockHoldRepo.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<StockHold>(holds));
        }

        private void SetupStockStream(params StockItem[] items)
        {
            _stockItemRepo
                .Setup(r => r.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable(items));
        }

        // ── GetByProductIdAsync

        [Fact]
        public async Task GetByProductIdAsync_StockExists_ShouldReturnDto()
        {
            // Arrange
            SetupAvailableStock(1, 10);

            // Act
            var result = await CreateService().GetByProductIdAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.ProductId.Should().Be(1);
            result.Quantity.Should().Be(10);
            result.AvailableQuantity.Should().Be(10);
        }

        [Fact]
        public async Task GetByProductIdAsync_StockNotFound_ShouldReturnNull()
        {
            // Arrange
            SetupStockMissing(99);

            // Act
            var result = await CreateService().GetByProductIdAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        // ── GetByProductIdsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdsAsync_AllIdsHaveStock_ShouldYieldAllDtos()
        {
            // Arrange
            var (s1, _) = StockItem.Create(new StockProductId(1), new StockQuantity(5));
            var (s2, _) = StockItem.Create(new StockProductId(2), new StockQuantity(3));
            SetupStockStream(s1, s2);

            // Act
            var result = new List<StockItemDto>();
            await foreach (var dto in CreateService().GetByProductIdsAsync(new[] { 1, 2 }, TestContext.Current.CancellationToken))
                result.Add(dto);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(d => d.ProductId == 1 && d.AvailableQuantity == 5);
            result.Should().Contain(d => d.ProductId == 2 && d.AvailableQuantity == 3);
        }

        [Fact]
        public async Task GetByProductIdsAsync_EmptyInput_ShouldYieldNothing()
        {
            // Arrange
            SetupStockStream();

            // Act
            var result = new List<StockItemDto>();
            await foreach (var dto in CreateService().GetByProductIdsAsync(System.Array.Empty<int>(), TestContext.Current.CancellationToken))
                result.Add(dto);

            // Assert
            result.Should().BeEmpty();
        }

        // ── InitializeStockAsync ──────────────────────────────────────────────

        [Fact]
        public async Task InitializeStockAsync_NewProduct_ShouldAddStock()
        {
            // Arrange
            SetupStockMissing(1);

            // Act
            var result = await CreateService().InitializeStockAsync(1, 20, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.AddAsync(It.Is<StockItem>(s => s.ProductId.Value == 1 && s.Quantity.Value == 20), It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 20),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeStockAsync_AlreadyInitialized_ShouldReturnFalse()
        {
            // Arrange
            SetupAvailableStock(1, 5);

            // Act
            var result = await CreateService().InitializeStockAsync(1, 10, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        // ── ReserveAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReserveAsync_PhysicalProduct_ShouldReserveStockAndCreateReservation()
        {
            // Arrange
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Orderable);
            SetupAvailableStock(1, 10);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            SetupProductSnapshot(1, snapshot);

            // Act
            var result = await CreateService().ReserveAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ReserveStockResult.Success);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.AddAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
            _deferredScheduler.Verify(s => s.ScheduleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 7),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReserveAsync_DigitalProduct_ShouldSkipStockUpdateButCreateReservation()
        {
            // Arrange
            var snapshot = ProductSnapshot.Create(1, "eBook", isDigital: true, CatalogProductStatus.Orderable);
            var dto = new ReserveStockDto(1, 42, 1, "user-1", DateTime.UtcNow.AddHours(1));

            SetupProductSnapshot(1, snapshot);

            // Act
            var result = await CreateService().ReserveAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ReserveStockResult.Success);
            _stockItemRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockHoldRepo.Verify(r => r.AddAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReserveAsync_SnapshotNotFound_ShouldReturnProductSnapshotNotFound()
        {
            // Arrange
            SetupProductSnapshotMissing(1);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            // Act
            var result = await CreateService().ReserveAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ReserveStockResult.ProductSnapshotNotFound);
        }

        [Fact]
        public async Task ReserveAsync_ProductNotOrderable_ShouldReturnProductNotAvailable()
        {
            // Arrange
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Suspended);
            SetupProductSnapshot(1, snapshot);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            // Act
            var result = await CreateService().ReserveAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(ReserveStockResult.ProductNotAvailable);
        }

        // ── ReleaseAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseAsync_GuaranteedReservation_ShouldReleaseStockAndDeleteReservation()
        {
            // Arrange
            var stockHold = SetupReservedStockHold(42, 1, 3, 3);

            // Act
            var result = await CreateService().ReleaseAsync(42, 1, 3, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 10),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReleaseAsync_ReservationNotFound_ShouldReturnFalse()
        {
            // Arrange
            SetupStockHoldMissing(99, 1);

            // Act
            var result = await CreateService().ReleaseAsync(99, 1, 3, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReleaseAsync_QuantityExceedsReserved_ShouldSkipStockUpdateButDeleteReservation()
        {
            // Arrange
            var stockHold = SetupReservedStockHold(42, 1, 5, 2);

            // Act
            var result = await CreateService().ReleaseAsync(42, 1, 5, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ConfirmAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_ExistingReservation_ShouldConfirmAndUpdate()
        {
            // Arrange
            var stockHold = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 3, DateTime.UtcNow.AddHours(1));
            SetupStockHold(42, 1, stockHold);

            // Act
            var result = await CreateService().ConfirmAsync(42, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            stockHold.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmAsync_ReservationNotFound_ShouldReturnFalse()
        {
            // Arrange
            SetupStockHoldMissing(99, 1);

            // Act
            var result = await CreateService().ConfirmAsync(99, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        // ── ConfirmReservationsByOrderAsync ───────────────────────────────────

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_MultipleReservations_ShouldConfirmAll()
        {
            // Arrange
            var r1 = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 3, DateTime.UtcNow.AddHours(1));
            var r2 = StockHold.Create(new StockProductId(2), new ReservationOrderId(42), 5, DateTime.UtcNow.AddHours(1));
            SetupHoldsByOrder(42, r1, r2);

            // Act
            await CreateService().ConfirmHoldsByOrderAsync(42, TestContext.Current.CancellationToken);

            // Assert
            r1.Status.Should().Be(StockHoldStatus.Confirmed);
            r2.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_NoReservations_ShouldNotUpdate()
        {
            // Arrange
            SetupHoldsByOrder(99);

            // Act
            await CreateService().ConfirmHoldsByOrderAsync(99, TestContext.Current.CancellationToken);

            // Assert
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_SingleReservation_ShouldConfirmAndUpdate()
        {
            // Arrange
            var stockHold = StockHold.Create(new StockProductId(5), new ReservationOrderId(10), 2, DateTime.UtcNow.AddHours(1));
            SetupHoldsByOrder(10, stockHold);

            // Act
            await CreateService().ConfirmHoldsByOrderAsync(10, TestContext.Current.CancellationToken);

            // Assert
            stockHold.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── FulfillAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task FulfillAsync_ValidStock_ShouldFulfillAndDeleteReservation()
        {
            // Arrange
            var stockHold = SetupReservedStockHold(42, 1, 5, 5);

            // Act
            var result = await CreateService().FulfillAsync(42, 1, 5, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FulfillAsync_StockNotFound_ShouldReturnFalse()
        {
            // Arrange
            SetupStockMissing(1);

            // Act
            var result = await CreateService().FulfillAsync(42, 1, 5, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task FulfillAsync_QuantityExceedsReserved_ShouldReturnFalse()
        {
            // Arrange
            var stock = SetupAvailableStock(1, 10);
            stock.Reserve(3);

            // Act
            var result = await CreateService().FulfillAsync(42, 1, 5, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ReturnAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsync_ValidStock_ShouldReturnQuantityAndUpdateStock()
        {
            // Arrange
            var stock = SetupAvailableStock(1, 10);
            stock.Reserve(5);
            stock.Fulfill(5);

            // Act
            var result = await CreateService().ReturnAsync(1, 3, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            stock.Quantity.Value.Should().Be(8);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReturnAsync_StockNotFound_ShouldReturnFalse()
        {
            // Arrange
            SetupStockMissing(1);

            // Act
            var result = await CreateService().ReturnAsync(1, 3, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        // ── WithdrawHoldAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task WithdrawHoldAsync_GuaranteedHold_ShouldReleaseStockEnqueueAndMarkWithdrawn()
        {
            // Arrange
            var stockHold = SetupReservedStockHold(42, 1, 3, 3);

            // Act
            var result = await CreateService().WithdrawHoldAsync(42, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            stockHold.Status.Should().Be(StockHoldStatus.Withdrawn);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<StockAvailabilityChanged>(m => m.ProductId == 1 && m.AvailableQuantity == 10),
                _txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WithdrawHoldAsync_HoldNotFound_ShouldReturnFalse_AndDoesNotOpenTransaction()
        {
            // Arrange
            SetupStockHoldMissing(99, 1);

            // Act
            var result = await CreateService().WithdrawHoldAsync(99, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithdrawHoldAsync_QuantityExceedsReserved_ShouldSkipStockReleaseButMarkWithdrawn()
        {
            // Arrange
            var stockHold = SetupReservedStockHold(42, 1, 5, 2);

            // Act
            var result = await CreateService().WithdrawHoldAsync(42, 1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            stockHold.Status.Should().Be(StockHoldStatus.Withdrawn);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── AdjustAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task AdjustAsync_ValidDto_ShouldUpsertAndCancelAndScheduleJob()
        {
            // Arrange
            var dto = new AdjustStockDto(1, 15);

            // Act
            await CreateService().AdjustAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            _pendingAdjustmentRepo.Verify(r => r.UpsertAsync(1, 15, It.IsAny<CancellationToken>()), Times.Once);
            _deferredScheduler.Verify(s => s.CancelAsync(
                StockAdjustmentJob.JobTaskName, "1", It.IsAny<CancellationToken>()), Times.Once);
                            _deferredScheduler.Verify(s => s.ScheduleAsync(
                            StockAdjustmentJob.JobTaskName, "1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
                    }

                    private static async IAsyncEnumerable<StockItem> AsAsyncEnumerable(params StockItem[] items)
                    {
                        foreach (var item in items)
                        {
                            yield return item;
                        }
                        await Task.CompletedTask;
                    }
                }
            }
