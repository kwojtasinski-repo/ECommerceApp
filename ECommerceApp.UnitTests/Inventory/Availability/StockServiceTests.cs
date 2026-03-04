using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class StockServiceTests
    {
        private readonly Mock<IStockItemRepository> _stockItemRepo;
        private readonly Mock<IReservationRepository> _reservationRepo;
        private readonly Mock<IProductSnapshotRepository> _productSnapshotRepo;
        private readonly Mock<IPendingStockAdjustmentRepository> _pendingAdjustmentRepo;
        private readonly Mock<ICheckoutSoftHoldService> _softHoldService;
        private readonly Mock<IDeferredJobScheduler> _deferredScheduler;

        public StockServiceTests()
        {
            _stockItemRepo = new Mock<IStockItemRepository>();
            _reservationRepo = new Mock<IReservationRepository>();
            _productSnapshotRepo = new Mock<IProductSnapshotRepository>();
            _pendingAdjustmentRepo = new Mock<IPendingStockAdjustmentRepository>();
            _softHoldService = new Mock<ICheckoutSoftHoldService>();
            _deferredScheduler = new Mock<IDeferredJobScheduler>();
        }

        private StockService CreateService() => new(
            _stockItemRepo.Object,
            _reservationRepo.Object,
            _productSnapshotRepo.Object,
            _pendingAdjustmentRepo.Object,
            _softHoldService.Object,
            _deferredScheduler.Object);

        // ── GetByProductIdAsync

        [Fact]
        public async Task GetByProductIdAsync_StockExists_ShouldReturnDto()
        {
            var (stock, _) = StockItem.Create(1, 10);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var result = await CreateService().GetByProductIdAsync(1);

            result.Should().NotBeNull();
            result.ProductId.Should().Be(1);
            result.Quantity.Should().Be(10);
            result.AvailableQuantity.Should().Be(10);
        }

        [Fact]
        public async Task GetByProductIdAsync_StockNotFound_ShouldThrowBusinessException()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var act = async () => await CreateService().GetByProductIdAsync(99);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*not found*");
        }

        // ── InitializeStockAsync ──────────────────────────────────────────────

        [Fact]
        public async Task InitializeStockAsync_NewProduct_ShouldAddStock()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            await CreateService().InitializeStockAsync(1, 20);

            _stockItemRepo.Verify(r => r.AddAsync(It.Is<StockItem>(s => s.ProductId == 1 && s.Quantity == 20), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeStockAsync_AlreadyInitialized_ShouldThrowBusinessException()
        {
            var (stock, _) = StockItem.Create(1, 5);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            var act = async () => await CreateService().InitializeStockAsync(1, 10);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*already initialized*");
        }

        // ── ReserveAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReserveAsync_PhysicalProduct_ShouldReserveStockAndCreateReservationAndScheduleTimeout()
        {
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Orderable);
            var (stock, _) = StockItem.Create(1, 10);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            await CreateService().ReserveAsync(dto);

            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
            _softHoldService.Verify(s => s.RemoveAsync(1, "user-1", It.IsAny<CancellationToken>()), Times.Once);
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

            await CreateService().ReserveAsync(dto);

            _stockItemRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
            _reservationRepo.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReserveAsync_SnapshotNotFound_ShouldThrowBusinessException()
        {
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductSnapshot)null);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            var act = async () => await CreateService().ReserveAsync(dto);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*snapshot not found*");
        }

        [Fact]
        public async Task ReserveAsync_ProductNotOrderable_ShouldThrowBusinessException()
        {
            var snapshot = ProductSnapshot.Create(1, "Widget", isDigital: false, CatalogProductStatus.Suspended);
            _productSnapshotRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            var dto = new ReserveStockDto(1, 42, 3, "user-1", DateTime.UtcNow.AddHours(1));

            var act = async () => await CreateService().ReserveAsync(dto);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*not available*");
        }

        // ── ReleaseAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseAsync_GuaranteedReservation_ShouldReleaseStockAndDeleteReservation()
        {
            var reservation = Reservation.Create(1, 42, 3, DateTime.UtcNow.AddHours(1));
            var (stock, _) = StockItem.Create(1, 10);
            stock.Reserve(3);

            _reservationRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            await CreateService().ReleaseAsync(42, 1, 3);

            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReleaseAsync_ReservationNotFound_ShouldReturnWithoutError()
        {
            _reservationRepo.Setup(r => r.GetByOrderAndProductAsync(99, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Reservation)null);

            var act = async () => await CreateService().ReleaseAsync(99, 1, 3);

            await act.Should().NotThrowAsync();
            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ConfirmAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_ExistingReservation_ShouldConfirmAndUpdate()
        {
            var reservation = Reservation.Create(1, 42, 3, DateTime.UtcNow.AddHours(1));
            _reservationRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);

            await CreateService().ConfirmAsync(42, 1);

            reservation.Status.Should().Be(ReservationStatus.Confirmed);
            _reservationRepo.Verify(r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmAsync_ReservationNotFound_ShouldReturnWithoutError()
        {
            _reservationRepo.Setup(r => r.GetByOrderAndProductAsync(99, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Reservation)null);

            var act = async () => await CreateService().ConfirmAsync(99, 1);

            await act.Should().NotThrowAsync();
        }

        // ── FulfillAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task FulfillAsync_ValidStock_ShouldFulfillAndDeleteReservation()
        {
            var (stock, _) = StockItem.Create(1, 10);
            stock.Reserve(5);
            var reservation = Reservation.Create(1, 42, 5, DateTime.UtcNow.AddHours(1));

            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
            _reservationRepo.Setup(r => r.GetByOrderAndProductAsync(42, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);

            await CreateService().FulfillAsync(42, 1, 5);

            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FulfillAsync_StockNotFound_ShouldThrowBusinessException()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var act = async () => await CreateService().FulfillAsync(42, 1, 5);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*not found*");
        }

        // ── ReturnAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsync_ValidStock_ShouldReturnQuantityAndUpdateStock()
        {
            var (stock, _) = StockItem.Create(1, 10);
            stock.Reserve(5);
            stock.Fulfill(5);
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);

            await CreateService().ReturnAsync(1, 3);

            _stockItemRepo.Verify(r => r.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
            stock.Quantity.Should().Be(8);
        }

        [Fact]
        public async Task ReturnAsync_StockNotFound_ShouldThrowBusinessException()
        {
            _stockItemRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockItem)null);

            var act = async () => await CreateService().ReturnAsync(1, 3);

            await act.Should().ThrowAsync<BusinessException>().WithMessage("*not found*");
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
    }
}
