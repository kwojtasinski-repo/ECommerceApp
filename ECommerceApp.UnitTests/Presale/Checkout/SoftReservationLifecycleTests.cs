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

        // ── Hold → expire lifecycle ──────────────────────────────────────────

        [Fact]
        public async Task Hold_ThenExpire_ReservationIsRemovedFromDbAndCache()
        {
            const int productId = 1;
            const string userId = "user-1";
            const int reservationId = 42;

            var snapshot = StockSnapshot.Create(productId, 10, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(49.99m);

            // Capture the reservation created during HoldAsync and assign an Id
            // (simulating what EF Core does after AddAsync in production).
            SoftReservation? capturedReservation = null;
            _reservationRepo.Setup(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Callback<SoftReservation, CancellationToken>((res, _) =>
                {
                    capturedReservation = res;
                    typeof(SoftReservation).GetProperty(nameof(SoftReservation.Id))!
                        .SetValue(res, new SoftReservationId(reservationId));
                })
                .Returns(Task.CompletedTask);

            string? scheduledEntityId = null;
            _deferredScheduler.Setup(d => d.ScheduleAsync(
                    SoftReservationExpiredJob.JobTaskName, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DateTime, CancellationToken>((_, entityId, _, _) => scheduledEntityId = entityId)
                .Returns(Task.CompletedTask);

            // Act — step 1: hold the reservation
            var held = await _service.HoldAsync(productId, userId, 2);

            held.Should().BeTrue();
            scheduledEntityId.Should().Be(reservationId.ToString());

            // Verify the reservation is in the cache after HoldAsync
            _cache.TryGetValue<SoftReservation>($"sr:{productId}:{userId}", out var cachedBeforeExpiry);
            cachedBeforeExpiry.Should().NotBeNull();

            // Act — step 2: the deferred job fires after 15 minutes
            _reservationRepo.Setup(r => r.GetByIdAsync(new SoftReservationId(reservationId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(capturedReservation!);

            var jobContext = new JobExecutionContext(scheduledEntityId, Guid.NewGuid().ToString());
            await _expiredJob.ExecuteAsync(jobContext, default);

            // Assert — reservation removed from DB and cache
            _reservationRepo.Verify(r => r.DeleteAsync(capturedReservation!, It.IsAny<CancellationToken>()), Times.Once);
            _cache.TryGetValue<SoftReservation>($"sr:{productId}:{userId}", out var cachedAfterExpiry);
            cachedAfterExpiry.Should().BeNull("the TTL job must evict the cache entry on expiry");
            jobContext.Outcome.Should().BeOfType<JobOutcome.Success>();
        }

        [Fact]
        public async Task Hold_ThenManualRemove_JobBecomesNoOp()
        {
            const int productId = 2;
            const string userId = "user-2";
            const int reservationId = 7;

            var snapshot = StockSnapshot.Create(productId, 5, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(20m);
            _reservationRepo.Setup(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Callback<SoftReservation, CancellationToken>((res, _) =>
                    typeof(SoftReservation).GetProperty(nameof(SoftReservation.Id))!
                        .SetValue(res, new SoftReservationId(reservationId)))
                .Returns(Task.CompletedTask);
            _deferredScheduler.Setup(d => d.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _deferredScheduler.Setup(d => d.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Hold the reservation
            await _service.HoldAsync(productId, userId, 1);

            // Simulate user removing reservation before the TTL fires (e.g. item removed from cart)
            var reservation = SoftReservation.Create(productId, userId, 1, 20m, DateTime.UtcNow.AddMinutes(15));
            _reservationRepo.Setup(r => r.FindAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.Is<PresaleUserId>(u => u.Value == userId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservation);
            _reservationRepo.Setup(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.RemoveAsync(productId, userId);

            // When the deferred job fires late, the reservation is already gone → no-op
            _reservationRepo.Setup(r => r.GetByIdAsync(new SoftReservationId(reservationId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SoftReservation)null!);

            var jobContext = new JobExecutionContext(reservationId.ToString(), Guid.NewGuid().ToString());
            await _expiredJob.ExecuteAsync(jobContext, default);

            jobContext.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("No-op");
        }

        [Fact]
        public async Task Hold_SchedulesJobWithReservationIdAsEntityId()
        {
            const int productId = 3;
            const string userId = "user-3";
            const int reservationId = 15;

            var snapshot = StockSnapshot.Create(productId, 10, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(99m);
            _reservationRepo.Setup(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Callback<SoftReservation, CancellationToken>((res, _) =>
                    typeof(SoftReservation).GetProperty(nameof(SoftReservation.Id))!
                        .SetValue(res, new SoftReservationId(reservationId)))
                .Returns(Task.CompletedTask);

            await _service.HoldAsync(productId, userId, 1);

            _deferredScheduler.Verify(d => d.ScheduleAsync(
                SoftReservationExpiredJob.JobTaskName,
                reservationId.ToString(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Hold_SchedulesJobWithTtlMatchingPresaleOptions()
        {
            const int productId = 4;
            const string userId = "user-4";
            var expectedTtl = TimeSpan.FromMinutes(15);
            var before = DateTime.UtcNow;

            var snapshot = StockSnapshot.Create(productId, 10, DateTime.UtcNow);
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
            _reservationRepo.Setup(r => r.GetByProductIdAsync(It.Is<PresaleProductId>(p => p.Value == productId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());
            _catalogClient.Setup(c => c.GetUnitPriceAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(10m);
            _reservationRepo.Setup(r => r.AddAsync(It.IsAny<SoftReservation>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.HoldAsync(productId, userId, 1);

            var after = DateTime.UtcNow;

            _deferredScheduler.Verify(d => d.ScheduleAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<DateTime>(dt => dt >= before.Add(expectedTtl) && dt <= after.Add(expectedTtl)),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
