using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
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
        private readonly Mock<IMessageBroker> _broker;
        private readonly Mock<IStockAuditRepository> _auditRepo;

        public StockServiceTests()
        {
            _stockItemRepo = new Mock<IStockItemRepository>();
            _stockHoldRepo = new Mock<IStockHoldRepository>();
            _productSnapshotRepo = new Mock<IProductSnapshotRepository>();
            _pendingAdjustmentRepo = new Mock<IPendingStockAdjustmentRepository>();
            _deferredScheduler = new Mock<IDeferredJobScheduler>();
            _broker = new Mock<IMessageBroker>();
            _auditRepo = new Mock<IStockAuditRepository>();
        }

        private StockService CreateService() => new(
            _stockItemRepo.Object,
            _stockHoldRepo.Object,
            _productSnapshotRepo.Object,
            _pendingAdjustmentRepo.Object,
            _deferredScheduler.Object,
            _broker.Object,
            _auditRepo.Object);

        // ── GetByProductIdAsync

        [Fact]
        public async Task GetByProductIdAsync_StockExists_ShouldReturnDto()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().GetByProductIdAsync(1);

            result.Should().NotBeNull();
            result.ProductId.Should().Be(1);
            result.Quantity.Should().Be(10);
            result.AvailableQuantity.Should().Be(10);
        }

        [Fact]
        public async Task GetByProductIdAsync_StockNotFound_ShouldReturnNull()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var result = await CreateService().GetByProductIdAsync(99);

            result.Should().BeNull();
        }

        // ── GetByProductIdsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdsAsync_AllIdsHaveStock_ShouldYieldAllDtos()
        {
            var (s1, _) = StockItem.Create(new StockProductId(1), new StockQuantity(5));
            var (s2, _) = StockItem.Create(new StockProductId(2), new StockQuantity(3));
            _stockItemRepo
                .Setup(r => r.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable(s1, s2));

            var result = new List<StockItemDto>();
            await foreach (var dto in CreateService().GetByProductIdsAsync(new[] { 1, 2 }))
                result.Add(dto);

            result.Should().HaveCount(2);
            result.Should().Contain(d => d.ProductId == 1 && d.AvailableQuantity == 5);
            result.Should().Contain(d => d.ProductId == 2 && d.AvailableQuantity == 3);
        }

        [Fact]
        public async Task GetByProductIdsAsync_EmptyInput_ShouldYieldNothing()
        {
            _stockItemRepo
                .Setup(r => r.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable());

            var result = new List<StockItemDto>();
            await foreach (var dto in CreateService().GetByProductIdsAsync(System.Array.Empty<int>()))
                result.Add(dto);

            result.Should().BeEmpty();
        }

        // ── InitializeStockAsync ──────────────────────────────────────────────

        [Fact]
        public async Task InitializeStockAsync_NewProduct_ShouldAddStock()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var result = await CreateService().InitializeStockAsync(1, 20);

            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.AddAsync(It.Is<StockItem>(s => s.ProductId.Value == 1 && s.Quantity.Value == 20), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeStockAsync_AlreadyInitialized_ShouldReturnFalse()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(5));
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().InitializeStockAsync(1, 10);

            result.Should().BeFalse();
        }

        // ── ReserveAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReserveAsync_PhysicalProduct_ShouldReserveStockAndCreateReservationAndScheduleTimeout()
        {
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Orderable);
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().ReserveAsync(dto);

            result.Should().Be(ReserveStockResult.Success);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.AddAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
            _deferredScheduler.Verify(s => s.ScheduleAsync(
                PaymentWindowTimeoutJob.JobTaskName,
                It.Is<string>(e => e.StartsWith("42:1:3")),
                dto.ExpiresAt, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReserveAsync_DigitalProduct_ShouldSkipStockUpdateButCreateReservation()
        {
            var snapshot = ProductSnapshot.Create(1, "eBook", isDigital: true, CatalogProductStatus.Orderable);
            var dto = new ReserveStockDto(1, 42, 1, "user-1", DateTime.UtcNow.AddHours(1));

            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);

            var result = await CreateService().ReserveAsync(dto);

            result.Should().Be(ReserveStockResult.Success);
            _stockItemRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockHoldRepo.Verify(r => r.AddAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReserveAsync_SnapshotNotFound_ShouldReturnProductSnapshotNotFound()
        {
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductSnapshot)null);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            var result = await CreateService().ReserveAsync(dto);

            result.Should().Be(ReserveStockResult.ProductSnapshotNotFound);
        }

        [Fact]
        public async Task ReserveAsync_ProductNotOrderable_ShouldReturnProductNotAvailable()
        {
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Suspended);
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            var result = await CreateService().ReserveAsync(dto);

            result.Should().Be(ReserveStockResult.ProductNotAvailable);
        }

        // ── ReleaseAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseAsync_GuaranteedReservation_ShouldReleaseStockAndDeleteReservation()
        {
            var stockHold = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 3, DateTime.UtcNow.AddHours(1));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(3);

            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockHold);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().ReleaseAsync(42, 1, 3);

            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReleaseAsync_ReservationNotFound_ShouldReturnFalse()
        {
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(99, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockHold)null);

            var result = await CreateService().ReleaseAsync(99, 1, 3);

            result.Should().BeFalse();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReleaseAsync_QuantityExceedsReserved_ShouldSkipStockUpdateButDeleteReservation()
        {
            var stockHold = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 5, DateTime.UtcNow.AddHours(1));
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(2);

            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockHold);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().ReleaseAsync(42, 1, 5);

            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── ConfirmAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_ExistingReservation_ShouldConfirmAndUpdate()
        {
            var stockHold = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 3, DateTime.UtcNow.AddHours(1));
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockHold);

            var result = await CreateService().ConfirmAsync(42, 1);

            result.Should().BeTrue();
            stockHold.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmAsync_ReservationNotFound_ShouldReturnFalse()
        {
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(99, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockHold)null);

            var result = await CreateService().ConfirmAsync(99, 1);

            result.Should().BeFalse();
        }

        // ── ConfirmReservationsByOrderAsync ───────────────────────────────────

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_MultipleReservations_ShouldConfirmAll()
        {
            var r1 = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 3, DateTime.UtcNow.AddHours(1));
            var r2 = StockHold.Create(new StockProductId(2), new ReservationOrderId(42), 5, DateTime.UtcNow.AddHours(1));
            _stockHoldRepo.Setup(r => r.GetByOrderIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<StockHold> { r1, r2 });

            await CreateService().ConfirmHoldsByOrderAsync(42);

            r1.Status.Should().Be(StockHoldStatus.Confirmed);
            r2.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_NoReservations_ShouldNotUpdate()
        {
            _stockHoldRepo.Setup(r => r.GetByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<StockHold>());

            await CreateService().ConfirmHoldsByOrderAsync(99);

            _stockHoldRepo.Verify(r => r.UpdateAsync(It.IsAny<StockHold>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmReservationsByOrderAsync_SingleReservation_ShouldConfirmAndUpdate()
        {
            var stockHold = StockHold.Create(new StockProductId(5), new ReservationOrderId(10), 2, DateTime.UtcNow.AddHours(1));
            _stockHoldRepo.Setup(r => r.GetByOrderIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<StockHold> { stockHold });

            await CreateService().ConfirmHoldsByOrderAsync(10);

            stockHold.Status.Should().Be(StockHoldStatus.Confirmed);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── FulfillAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task FulfillAsync_ValidStock_ShouldFulfillAndDeleteReservation()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);
            var stockHold = StockHold.Create(new StockProductId(1), new ReservationOrderId(42), 5, DateTime.UtcNow.AddHours(1));

            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
            _stockHoldRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockHold);

            var result = await CreateService().FulfillAsync(42, 1, 5);

            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _stockHoldRepo.Verify(r => r.UpdateAsync(stockHold, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FulfillAsync_StockNotFound_ShouldReturnFalse()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var result = await CreateService().FulfillAsync(42, 1, 5);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task FulfillAsync_QuantityExceedsReserved_ShouldReturnFalse()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(3);

            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().FulfillAsync(42, 1, 5);

            result.Should().BeFalse();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ReturnAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsync_ValidStock_ShouldReturnQuantityAndUpdateStock()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);
            stock.Fulfill(5);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().ReturnAsync(1, 3);

            result.Should().BeTrue();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            stock.Quantity.Value.Should().Be(8);
        }

        [Fact]
        public async Task ReturnAsync_StockNotFound_ShouldReturnFalse()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var result = await CreateService().ReturnAsync(1, 3);

            result.Should().BeFalse();
        }

        // ── AdjustAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task AdjustAsync_ValidDto_ShouldUpsertAndCancelAndScheduleJob()
        {
            var dto = new AdjustStockDto(1, 15);

            await CreateService().AdjustAsync(dto);

            _pendingAdjustmentRepo.Verify(r => r.UpsertAsync(1, 15, It.IsAny<CancellationToken>()), Times.Once);
            _deferredScheduler.Verify(s => s.CancelAsync(
                StockAdjustmentJob.JobTaskName, "1", It.IsAny<CancellationToken>()), Times.Once);
                            _deferredScheduler.Verify(s => s.ScheduleAsync(
                            StockAdjustmentJob.JobTaskName, "1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
                    }

                    private static async IAsyncEnumerable<StockItem> AsAsyncEnumerable(params StockItem[] items)
                    {
                        foreach (var item in items)
                            yield return item;
                        await Task.CompletedTask;
                    }
                }
            }
