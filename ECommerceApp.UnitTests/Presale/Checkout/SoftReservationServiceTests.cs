using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Presale.Checkout;
using FluentAssertions;
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
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockSnapshot)null!);

            var result = await _service.HoldAsync(1, "user-1", 2);

            result.Should().BeFalse();
            _reservationRepo.Verify(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HoldAsync_InsufficientAvailableStock_ShouldReturnFalse()
        {
            var snapshot = StockSnapshot.Create(1, 3, DateTime.UtcNow);
            var existing = new List<SoftReservation>
            {
                SoftReservation.Create(1, "other-user", 2, 10m, DateTime.UtcNow.AddMinutes(10))
            };

            _snapshotRepo.Setup(r => r.FindByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var result = await _service.HoldAsync(1, "user-1", 3); // 3 - 2 = 1 available, needs 3

            result.Should().BeFalse();
        }

        [Fact]
        public async Task HoldAsync_UnitPriceNotFound_ShouldReturnFalse()
        {
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);

            _snapshotRepo.Setup(r => r.FindByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

            var result = await _service.HoldAsync(1, "user-1", 2);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task HoldAsync_AllConditionsMet_ShouldPersistScheduleCacheAndReturnTrue()
        {
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(49.99m);
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _service.HoldAsync(1, "user-1", 2);

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
            var snapshot = StockSnapshot.Create(1, 10, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(10m);
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.HoldAsync(1, "user-1", 1);

            var cached = await _service.GetAsync(1, "user-1");
            cached.Should().NotBeNull();
            cached!.ProductId.Value.Should().Be(1);
        }

        // ── GetAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAsync_ReservationInCache_ShouldReturnFromCacheWithoutDbCall()
        {
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            _cache.Set("sr:1:user-1", reservation, TimeSpan.FromMinutes(15));

            var result = await _service.GetAsync(1, "user-1");

            result.Should().NotBeNull();
            result!.ProductId.Value.Should().Be(1);
            _reservationRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<PresaleProductId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAsync_ReservationNotInCache_ShouldReturnNull()
        {
            var result = await _service.GetAsync(99, "nobody");

            result.Should().BeNull();
        }

        // ── RemoveAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ReservationExists_ShouldCancelJobAndDeleteFromDb()
        {
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            _reservationRepo.Setup(r => r.FindAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.Is<PresaleUserId>(u => u.Value == "user-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
            _deferredScheduler.Setup(d => d.CancelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.RemoveAsync(1, "user-1");

            _deferredScheduler.Verify(d => d.CancelAsync(
                SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_ReservationNotExists_ShouldNotCallDeleteOrCancel()
        {
            _reservationRepo.Setup(r => r.FindAsync(99, "nobody", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SoftReservation)null!);

            var act = async () => await _service.RemoveAsync(99, "nobody");

            await act.Should().NotThrowAsync();
            _deferredScheduler.Verify(d => d.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── RemoveAllForProductAsync ───────────────────────────────────────────

        [Fact]
        public async Task RemoveAllForProductAsync_MultipleReservations_ShouldCancelAllJobsAndDeleteAll()
        {
            var reservations = new List<SoftReservation>
            {
                SoftReservation.Create(1, "user-1", 1, 10m, DateTime.UtcNow.AddMinutes(10)),
                SoftReservation.Create(1, "user-2", 2, 10m, DateTime.UtcNow.AddMinutes(10))
            };
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservations);
            _deferredScheduler.Setup(d => d.CancelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.RemoveAllForProductAsync(1);

            _deferredScheduler.Verify(d => d.CancelAsync(
                SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _reservationRepo.Verify(r => r.DeleteAllForProductAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAllForProductAsync_NoReservations_ShouldNotThrow()
        {
            _reservationRepo.Setup(r => r.GetByProductIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());

            var act = async () => await _service.RemoveAllForProductAsync(1);

            await act.Should().NotThrowAsync();
        }
    }
}
