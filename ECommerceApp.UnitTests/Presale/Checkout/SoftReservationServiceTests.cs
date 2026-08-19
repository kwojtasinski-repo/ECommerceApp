using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class SoftReservationServiceTests : IDisposable
    {
        private readonly Mock<ISoftReservationRepository> _reservationRepo;
        private readonly Mock<IStockSnapshotRepository> _snapshotRepo;
        private readonly Mock<ICatalogClient> _catalogClient;
        private readonly Mock<IDeferredJobScheduler> _deferredScheduler;
        private readonly IMemoryCache _cache;
        private readonly SoftReservationService _service;

        public SoftReservationServiceTests()
        {
            _reservationRepo = new Mock<ISoftReservationRepository>();
            _snapshotRepo = new Mock<IStockSnapshotRepository>();
            _catalogClient = new Mock<ICatalogClient>();
            _deferredScheduler = new Mock<IDeferredJobScheduler>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            var options = new Mock<IOptionsMonitor<PresaleOptions>>();
            options.Setup(o => o.CurrentValue).Returns(new PresaleOptions());

            _service = new SoftReservationService(
                _reservationRepo.Object,
                _snapshotRepo.Object,
                _catalogClient.Object,
                _deferredScheduler.Object,
                _cache,
                options.Object);
        }

        public void Dispose() => _cache.Dispose();

        // ── HoldAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task HoldAsync_StockSnapshotNotFound_ShouldReturnFalse()
        {
            // Arrange
            SetupStockSnapshot(1, null);

            // Act
            var result = await _service.HoldAsync(1, "user-1", 2, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
            _reservationRepo.Verify(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HoldAsync_InsufficientAvailableStock_ShouldReturnFalse()
        {
            // Arrange
            var snapshot = StockSnapshot.Create(1, 3, DateTime.UtcNow);
            var existing = new List<SoftReservation>
            {
                SoftReservation.Create(1, "other-user", 2, 10m, DateTime.UtcNow.AddMinutes(10))
            };

            SetupStockSnapshot(1, snapshot);
            SetupReservationsForProduct(1, existing);

            // Act
            var result = await _service.HoldAsync(1, "user-1", 3, TestContext.Current.CancellationToken); // 3 - 2 = 1 available, needs 3

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HoldAsync_UnitPriceNotFound_ShouldReturnFalse()
        {
            // Arrange
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);

            SetupStockSnapshot(1, snapshot);
            SetupReservationsForProduct(1, new List<SoftReservation>());
            SetupUnitPrice(1, null);

            // Act
            var result = await _service.HoldAsync(1, "user-1", 2, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HoldAsync_AllConditionsMet_ShouldPersistScheduleCacheAndReturnTrue()
        {
            // Arrange
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);
            SetupStockSnapshot(1, snapshot);
            SetupReservationsForProduct(1, new List<SoftReservation>());
            SetupUnitPrice(1, 49.99m);
            SetupSchedule();

            // Act
            var result = await _service.HoldAsync(1, "user-1", 2, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeTrue();
            _reservationRepo.Verify(r => r.AddAsync(
                It.Is<SoftReservation>(s => s.ProductId.Value == 1
                                         && s.UserId.Value == "user-1"
                                         && s.Quantity.Value == 2
                                         && s.UnitPrice.Amount == 49.99m),
                It.IsAny<CancellationToken>()), Times.Once);
            _deferredScheduler.Verify(d => d.ScheduleAsync(
                SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HoldAsync_AllConditionsMet_ShouldStoreReservationInCache()
        {
            // Arrange
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);
            SetupStockSnapshot(1, snapshot);
            SetupReservationsForProduct(1, new List<SoftReservation>());
            SetupUnitPrice(1, 10m);
            SetupSchedule();

            // Act
            await _service.HoldAsync(1, "user-1", 1, TestContext.Current.CancellationToken);

            // Assert
            var cached = await _service.GetAsync(1, "user-1", TestContext.Current.CancellationToken);
            cached.Should().NotBeNull();
            cached!.ProductId.Value.Should().Be(1);
        }

        // ── GetAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAsync_ReservationInCache_ShouldReturnFromCacheWithoutDbCall()
        {
            // Arrange
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            _cache.Set("sr:1:user-1", reservation, TimeSpan.FromMinutes(15));

            // Act
            var result = await _service.GetAsync(1, "user-1", TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.ProductId.Value.Should().Be(1);
            _reservationRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<PresaleProductId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAsync_ReservationNotInCache_ShouldReturnNull()
        {
            // Arrange

            // Act
            var result = await _service.GetAsync(99, "nobody", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        // ── RemoveAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ReservationExists_ShouldCancelJobAndDeleteFromDb()
        {
            // Arrange
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            SetupReservation(1, "user-1", reservation);
            SetupCancel();

            // Act
            await _service.RemoveAsync(1, "user-1", TestContext.Current.CancellationToken);

            // Assert
            _deferredScheduler.Verify(d => d.CancelAsync(
                SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_ReservationNotExists_ShouldNotCallDeleteOrCancel()
        {
            // Arrange
            SetupReservation(99, "nobody", null);

            // Act
            var act = async () => await _service.RemoveAsync(99, "nobody", TestContext.Current.CancellationToken);

            // Assert
            await act.Should().NotThrowAsync();
            _deferredScheduler.Verify(d => d.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── RemoveAllForProductAsync ───────────────────────────────────────────

        [Fact]
        public async Task RemoveAllForProductAsync_MultipleReservations_ShouldCancelAllJobsAndDeleteAll()
        {
            // Arrange
            var reservations = new List<SoftReservation>
            {
                SoftReservation.Create(1, "user-1", 1, 10m, DateTime.UtcNow.AddMinutes(10)),
                SoftReservation.Create(1, "user-2", 2, 10m, DateTime.UtcNow.AddMinutes(10))
            };
            SetupReservationsForProduct(1, reservations);
            SetupCancel();

            // Act
            await _service.RemoveAllForProductAsync(1, TestContext.Current.CancellationToken);

            // Assert
            _deferredScheduler.Verify(d => d.CancelAsync(
                SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _reservationRepo.Verify(r => r.DeleteAllForProductAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAllForProductAsync_NoReservations_ShouldNotThrow()
        {
            // Arrange
            SetupReservationsForProduct(1, new List<SoftReservation>());

            // Act
            var act = async () => await _service.RemoveAllForProductAsync(1, TestContext.Current.CancellationToken);

            // Assert
            await act.Should().NotThrowAsync();
        }

        // ── InvalidateExcessForProductAsync ──────────────────────────────────

        [Fact]
        public async Task InvalidateExcessForProductAsync_UnderCapacity_ShouldNotDeleteReservations()
        {
            // Arrange
            var reservations = new List<SoftReservation>
            {
                SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(10))
            };
            SetupActiveReservationsForProduct(1, reservations);

            // Act
            await _service.InvalidateExcessForProductAsync(1, 2, TestContext.Current.CancellationToken);

            // Assert
            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()), Times.Never);
            _deferredScheduler.Verify(d => d.CancelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InvalidateExcessForProductAsync_OverCapacity_ShouldRemoveNewestReservationsFirst()
        {
            // Arrange
            var oldest = SoftReservation.Create(1, "oldest", 2, 10m, DateTime.UtcNow.AddMinutes(10));
            var newest = SoftReservation.Create(1, "newest", 2, 10m, DateTime.UtcNow.AddMinutes(20));
            var reservations = new List<SoftReservation> { oldest, newest };
            SetupActiveReservationsForProduct(1, reservations);

            // Act
            await _service.InvalidateExcessForProductAsync(1, 2, TestContext.Current.CancellationToken);

            // Assert
            _reservationRepo.Verify(r => r.DeleteAsync(newest, It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(oldest, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InvalidateExcessForProductAsync_ShortfallRequiresAll_ShouldRemoveAllActiveReservations()
        {
            // Arrange
            var first = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(10));
            var second = SoftReservation.Create(1, "user-2", 2, 10m, DateTime.UtcNow.AddMinutes(20));
            SetupActiveReservationsForProduct(1, new[] { first, second });

            // Act
            await _service.InvalidateExcessForProductAsync(1, 0, TestContext.Current.CancellationToken);

            // Assert
            _reservationRepo.Verify(r => r.DeleteAsync(first, It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(second, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateExcessForProductAsync_ActiveQueryExcludesCommittedReservations()
        {
            // Arrange
            var active = SoftReservation.Create(1, "active", 3, 10m, DateTime.UtcNow.AddMinutes(10));
            var committed = SoftReservation.Create(1, "committed", 3, 10m, DateTime.UtcNow.AddMinutes(20));
            committed.Commit();
            SetupActiveReservationsForProduct(1, new[] { active });

            // Act
            await _service.InvalidateExcessForProductAsync(1, 2, TestContext.Current.CancellationToken);

            // Assert
            _reservationRepo.Verify(r => r.DeleteAsync(active, It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(committed, It.IsAny<CancellationToken>()), Times.Never);
        }

        private void SetupStockSnapshot(int productId, StockSnapshot? snapshot)
        {
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
        }

        private void SetupReservationsForProduct(int productId, IReadOnlyList<SoftReservation> reservations)
        {
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservations);
        }

        private void SetupActiveReservationsForProduct(int productId, IReadOnlyList<SoftReservation> reservations)
        {
            _reservationRepo.Setup(r => r.GetActiveByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservations);
        }

        private void SetupUnitPrice(int productId, decimal? price)
        {
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(price);
        }

        private void SetupSchedule()
        {
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupCancel()
        {
            _deferredScheduler.Setup(d => d.CancelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupReservation(int productId, string userId, SoftReservation? reservation)
        {
            _reservationRepo.Setup(r => r.FindAsync(productId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
        }
    }
}
