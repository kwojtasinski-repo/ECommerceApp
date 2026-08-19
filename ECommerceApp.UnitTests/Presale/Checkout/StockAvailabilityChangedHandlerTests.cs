using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class StockAvailabilityChangedHandlerTests
    {
        private readonly Mock<IStockSnapshotRepository> _snapshotRepo;
        private readonly StockAvailabilityChangedHandler _handler;

        public StockAvailabilityChangedHandlerTests()
        {
            _snapshotRepo = new Mock<IStockSnapshotRepository>();
            _handler = new StockAvailabilityChangedHandler(_snapshotRepo.Object, new Mock<IMemoryCache>().Object);
        }

        private void SetupSnapshotLookup(int productId, StockSnapshot snapshot)
        {
            _snapshotRepo.Setup(r => r.FindByProductIdAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);
        }

        [Fact]
        public async Task HandleAsync_SnapshotNotExists_ShouldCreateNewSnapshot()
        {
            SetupSnapshotLookup(1, null!);
            var message = new StockAvailabilityChanged(1, 50, DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _snapshotRepo.Verify(r => r.AddAsync(
                It.Is<StockSnapshot>(s => s.ProductId.Value == 1 && s.AvailableQuantity == 50),
                It.IsAny<CancellationToken>()), Times.Once);
            _snapshotRepo.Verify(r => r.UpdateAsync(It.IsAny<StockSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SnapshotExists_ShouldUpdateExistingSnapshotAndNotCreate()
        {
            var snapshot = StockSnapshot.Create(1, 100, DateTime.UtcNow.AddMinutes(-5));
            SetupSnapshotLookup(1, snapshot);
            var message = new StockAvailabilityChanged(1, 80, DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            snapshot.AvailableQuantity.Should().Be(80);
            _snapshotRepo.Verify(r => r.UpdateAsync(snapshot, It.IsAny<CancellationToken>()), Times.Once);
            _snapshotRepo.Verify(r => r.AddAsync(It.IsAny<StockSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SnapshotExists_ShouldUpdateLastSyncedAt()
        {
            var originalSyncedAt = DateTime.UtcNow.AddMinutes(-10);
            var snapshot = StockSnapshot.Create(1, 100, originalSyncedAt);
            SetupSnapshotLookup(1, snapshot);
            var newOccurredAt = DateTime.UtcNow;
            var message = new StockAvailabilityChanged(1, 60, newOccurredAt);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            snapshot.LastSyncedAt.Should().Be(newOccurredAt);
        }

        [Fact]
        public async Task HandleAsync_ZeroAvailableQuantity_ShouldBeStoredCorrectly()
        {
            SetupSnapshotLookup(1, null!);
            var message = new StockAvailabilityChanged(1, 0, DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _snapshotRepo.Verify(r => r.AddAsync(
                It.Is<StockSnapshot>(s => s.AvailableQuantity == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
