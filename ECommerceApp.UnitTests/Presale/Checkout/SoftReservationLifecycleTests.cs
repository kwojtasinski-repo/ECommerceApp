using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
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
    // Tests the connection between SoftReservationService (hold) and
    // SoftReservationExpiredJob (TTL expiry) — the two ends of the 15-minute
    // reservation window that closes before checkout is initiated.
    public class SoftReservationLifecycleTests : IDisposable
    {
        private readonly Mock<ISoftReservationRepository> _reservationRepo;
        private readonly Mock<IStockSnapshotRepository> _snapshotRepo;
        private readonly Mock<ICatalogClient> _catalogClient;
        private readonly Mock<IDeferredJobScheduler> _deferredScheduler;
        private readonly IMemoryCache _cache;
        private readonly SoftReservationService _service;
        private readonly SoftReservationExpiredJob _expiredJob;

        public SoftReservationLifecycleTests()
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

            _expiredJob = new SoftReservationExpiredJob(_reservationRepo.Object, _cache);
        }

        public void Dispose() => _cache.Dispose();

        private void SetupAvailableProductForReservation(int productId, int availableQuantity, decimal unitPrice)
        {
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(
                    It.Is<PresaleProductId>(product => product.Value == productId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(StockSnapshot.Create(productId, availableQuantity, DateTime.UtcNow));
            _reservationRepo.Setup(r => r.GetByProductIdAsync(
                    It.Is<PresaleProductId>(product => product.Value == productId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(unitPrice);
        }

        private void SetupReservationPersistence(int reservationId, Action<SoftReservation> onAdded = null)
        {
            _reservationRepo.Setup(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Callback<SoftReservation, CancellationToken>((reservation, _) =>
                {
                    EntityIdSetter.Set(reservation, new SoftReservationId(reservationId));
                    onAdded?.Invoke(reservation);
                })
                .Returns(Task.CompletedTask);
        }

        private void SetupSchedule()
        {
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupScheduleCapture(Action<string> onScheduled)
        {
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                SoftReservationExpiredJob.JobTaskName,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime, CancellationToken>((_, entityId, _, _) => onScheduled(entityId))
            .Returns(Task.CompletedTask);
        }

        private void SetupCancellation()
        {
            _deferredScheduler.Setup(d => d.CancelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupReservationLookup(int productId, string userId, SoftReservation reservation)
        {
            _reservationRepo.Setup(r => r.FindAsync(
                    It.Is<PresaleProductId>(product => product.Value == productId),
                    It.Is<PresaleUserId>(user => user.Value == userId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
        }

        private void SetupReservationCapture(int reservationId, Action<SoftReservation> onAdded)
        {
            SetupReservationPersistence(reservationId, onAdded);
        }

        private void SetupReservationById(int reservationId, SoftReservation reservation)
        {
            _reservationRepo.Setup(r => r.GetByIdAsync(
                    new SoftReservationId(reservationId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
        }

        private void SetupAnyReservationDeletion()
        {
            _reservationRepo.Setup(r => r.DeleteAsync(
                    It.IsAny<SoftReservation>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupReservationDeletion(SoftReservation reservation)
        {
            _reservationRepo.Setup(r => r.DeleteAsync(reservation, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // ── Hold → expire lifecycle ──────────────────────────────────────────

        [Fact]
        public async Task Hold_ThenExpire_ReservationIsRemovedFromDbAndCache()
        {
            // Arrange
            const int productId = 1;
            const string userId = "user-1";
            const int reservationId = 42;

            SetupAvailableProductForReservation(productId, 10, 49.99m);

            SoftReservation capturedReservation = null;
            SetupReservationCapture(reservationId, reservation => capturedReservation = reservation);

            string scheduledEntityId = null;
            SetupScheduleCapture(entityId => scheduledEntityId = entityId);

            // Act — step 1: hold the reservation
            var held = await _service.HoldAsync(productId, userId, 2, TestContext.Current.CancellationToken);

            // Assert — reservation is held and cached
            held.Should().BeTrue();
            scheduledEntityId.Should().Be(reservationId.ToString());

            _cache.TryGetValue<SoftReservation>($"sr:{productId}:{userId}", out var cachedBeforeExpiry);
            cachedBeforeExpiry.Should().NotBeNull();

            // Act — step 2: the deferred job fires after 15 minutes
            SetupReservationById(reservationId, capturedReservation!);

            var jobContext = new JobExecutionContext(scheduledEntityId, Guid.NewGuid().ToString());
            await _expiredJob.ExecuteAsync(jobContext, TestContext.Current.CancellationToken);

            // Assert — reservation removed from DB and cache
            _reservationRepo.Verify(r => r.DeleteAsync(capturedReservation!, It.IsAny<CancellationToken>()), Times.Once);
            _cache.TryGetValue<SoftReservation>($"sr:{productId}:{userId}", out var cachedAfterExpiry);
            cachedAfterExpiry.Should().BeNull("the TTL job must evict the cache entry on expiry");
            jobContext.Outcome.Should().BeOfType<JobOutcome.Success>();
        }

        [Fact]
        public async Task Hold_ThenManualRemove_JobBecomesNoOp()
        {
            // Arrange
            const int productId = 2;
            const string userId = "user-2";
            const int reservationId = 7;

            SetupAvailableProductForReservation(productId, 5, 20m);
            SetupReservationPersistence(reservationId);
            SetupSchedule();
            SetupCancellation();

            // Act — step 1: hold and manually remove the reservation
            await _service.HoldAsync(productId, userId, 1, TestContext.Current.CancellationToken);

            // Simulate user removing reservation before the TTL fires (e.g. item removed from cart)
            var reservation = SoftReservation.Create(productId, userId, 1, 20m, DateTime.UtcNow.AddMinutes(15));
            SetupReservationLookup(productId, userId, reservation);
            SetupAnyReservationDeletion();

            await _service.RemoveAsync(productId, userId, TestContext.Current.CancellationToken);

            // Act — step 2: execute the late expiry job
            SetupReservationById(reservationId, null);

            var jobContext = new JobExecutionContext(reservationId.ToString(), Guid.NewGuid().ToString());
            await _expiredJob.ExecuteAsync(jobContext, TestContext.Current.CancellationToken);

            // Assert
            jobContext.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("No-op");
        }

        [Fact]
        public async Task Hold_StoresReservationAndSchedulesExpiry()
        {
            const int productId = 3;
            const string userId = "user-3";
            const int reservationId = 15;

            // Arrange
            SetupAvailableProductForReservation(productId, 10, 99m);
            SetupReservationPersistence(reservationId);
            SetupSchedule();

            // Act
            var held = await _service.HoldAsync(productId, userId, 1, TestContext.Current.CancellationToken);

            // Assert
            held.Should().BeTrue();
            var cached = await _service.GetAsync(productId, userId, TestContext.Current.CancellationToken);
            cached.Should().NotBeNull();
            cached!.ProductId.Value.Should().Be(productId);
            // Assert
            _deferredScheduler.Verify(d => d.ScheduleAsync(
                SoftReservationExpiredJob.JobTaskName,
                reservationId.ToString(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Remove_CancelsExpiryDeletesReservationAndClearsCache()
        {
            const int productId = 4;
            const string userId = "user-4";
            var reservation = SoftReservation.Create(productId, userId, 1, 10m, DateTime.UtcNow.AddMinutes(15));
            EntityIdSetter.Set(reservation, new SoftReservationId(18));

            // Arrange
            SetupReservationLookup(productId, userId, reservation);
            SetupReservationDeletion(reservation);
            SetupCancellation();
            _cache.Set($"sr:{productId}:{userId}", reservation);

            // Act
            await _service.RemoveAsync(productId, userId, TestContext.Current.CancellationToken);

            // Assert
            _deferredScheduler.Verify(d => d.CancelAsync(
                SoftReservationExpiredJob.JobTaskName,
                reservation.Id!.Value.ToString(),
                It.IsAny<CancellationToken>()), Times.Once);
            _reservationRepo.Verify(r => r.DeleteAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
            _cache.TryGetValue<SoftReservation>($"sr:{productId}:{userId}", out _).Should().BeFalse();
        }

        [Fact]
        public async Task Hold_SchedulesExpiryAfterReservationTtlAndGracePeriod()
        {
            const int productId = 5;
            const string userId = "user-5";
            const int reservationId = 18;
            var expectedJobDelay = TimeSpan.FromMinutes(15) + TimeSpan.FromMinutes(1);
            var before = DateTime.UtcNow;

            // Arrange
            SetupAvailableProductForReservation(productId, 10, 10m);
            SetupReservationPersistence(reservationId);
            SetupSchedule();

            // Act
            await _service.HoldAsync(productId, userId, 1, TestContext.Current.CancellationToken);

            var after = DateTime.UtcNow;

            // Assert
            _deferredScheduler.Verify(d => d.ScheduleAsync(
                SoftReservationExpiredJob.JobTaskName,
                reservationId.ToString(),
                It.Is<DateTime>(scheduledAt => scheduledAt >= before.Add(expectedJobDelay) && scheduledAt <= after.Add(expectedJobDelay)),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
