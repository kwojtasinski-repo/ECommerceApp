using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
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
    public class GetPriceChangesTests : IDisposable
    {
        private readonly Mock<ISoftReservationRepository> _reservationRepo = new();
        private readonly Mock<IStockSnapshotRepository> _snapshotRepo = new();
        private readonly Mock<ICatalogClient> _catalogClient = new();
        private readonly Mock<IDeferredJobScheduler> _deferredScheduler = new();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly ISoftReservationService _sut;

        private static readonly PresaleUserId UserId = new("user-1");

        public GetPriceChangesTests()
        {
            var options = new Mock<IOptionsMonitor<PresaleOptions>>();
            options.Setup(o => o.CurrentValue).Returns(new PresaleOptions());

            _sut = new SoftReservationService(
                _reservationRepo.Object,
                _snapshotRepo.Object,
                _catalogClient.Object,
                _deferredScheduler.Object,
                _cache,
                options.Object);
        }

        public void Dispose() => _cache.Dispose();

        private static int _nextId = 1;

        private static SoftReservation MakeReservation(int productId, decimal unitPrice)
        {
            var r = SoftReservation.Create(productId, "user-1", 1, unitPrice, DateTime.UtcNow.AddMinutes(15));
            typeof(SoftReservation).GetProperty("Id")!.SetValue(r, new SoftReservationId(_nextId++));
            return r;
        }

        // ── GetAllForUserAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetAllForUserAsync_ReturnsAllReservationsForUser()
        {
            var reservations = new List<SoftReservation>
            {
                MakeReservation(productId: 1, unitPrice: 10m),
                MakeReservation(productId: 2, unitPrice: 20m)
            };
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservations);

            var result = await _sut.GetAllForUserAsync(UserId);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllForUserAsync_NoReservations_ReturnsEmpty()
        {
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());

            var result = await _sut.GetAllForUserAsync(UserId);

            result.Should().BeEmpty();
        }

        // ── GetPriceChangesAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetPriceChangesAsync_NoPriceChange_ReturnsEmpty()
        {
            const decimal price = 50m;
            var reservation = MakeReservation(productId: 1, unitPrice: price);
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(price);

            var result = await _sut.GetPriceChangesAsync(UserId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPriceChangesAsync_PriceRaised_ReturnsVmWithBothPrices()
        {
            var reservation = MakeReservation(productId: 2, unitPrice: 40m);
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(60m);

            var result = await _sut.GetPriceChangesAsync(UserId);

            result.Should().ContainSingle()
                .Which.Should().Match<Application.Presale.Checkout.ViewModels.SoftReservationPriceChangeVm>(
                    vm => vm.ProductId == 2 && vm.LockedPrice == 40m && vm.CurrentPrice == 60m);
        }

        [Fact]
        public async Task GetPriceChangesAsync_PriceDropped_ReturnsVmWithBothPrices()
        {
            var reservation = MakeReservation(productId: 3, unitPrice: 100m);
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(80m);

            var result = await _sut.GetPriceChangesAsync(UserId);

            result.Should().ContainSingle()
                .Which.Should().Match<Application.Presale.Checkout.ViewModels.SoftReservationPriceChangeVm>(
                    vm => vm.LockedPrice == 100m && vm.CurrentPrice == 80m);
        }

        [Fact]
        public async Task GetPriceChangesAsync_OnlyChangedLinesReturned()
        {
            var unchanged = MakeReservation(productId: 1, unitPrice: 20m);
            var changed = MakeReservation(productId: 2, unitPrice: 30m);
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { unchanged, changed });
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(20m);
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(35m);

            var result = await _sut.GetPriceChangesAsync(UserId);

            result.Should().ContainSingle()
                .Which.ProductId.Should().Be(2);
        }

        [Fact]
        public async Task GetPriceChangesAsync_CatalogReturnsNull_LineExcluded()
        {
            var reservation = MakeReservation(productId: 4, unitPrice: 25m);
            _reservationRepo
                .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _catalogClient
                .Setup(c => c.GetUnitPriceAsync(4, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

            var result = await _sut.GetPriceChangesAsync(UserId);

            result.Should().BeEmpty();
        }
    }
}
