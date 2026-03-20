using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Inventory.Availability;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class ProductUnpublishedHandlerTests
    {
        private readonly Mock<IProductSnapshotRepository> _snapshotRepo;
        private readonly ProductUnpublishedHandler _handler;

        public ProductUnpublishedHandlerTests()
        {
            _snapshotRepo = new Mock<IProductSnapshotRepository>();
            _handler = new ProductUnpublishedHandler(_snapshotRepo.Object);
        }

        [Fact]
        public async Task HandleAsync_ExistingSnapshot_ShouldUpsertWithSuspendedStatus()
        {
            var existing = ProductSnapshot.Create(42, "Widget", false, CatalogProductStatus.Orderable);
            _snapshotRepo.Setup(r => r.GetByProductIdAsync(42, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(existing);

            var message = new ProductUnpublished(ProductId: 42, Reason: UnpublishReason.ManualReview, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.Is<ProductSnapshot>(s =>
                    s.ProductId == 42 &&
                    s.ProductName == "Widget" &&
                    s.IsDigital == false &&
                    s.CatalogStatus == CatalogProductStatus.Suspended),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_NoExistingSnapshot_ShouldNotUpsert()
        {
            _snapshotRepo.Setup(r => r.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
                         .ReturnsAsync((ProductSnapshot?)null);

            var message = new ProductUnpublished(ProductId: 99, Reason: UnpublishReason.Other, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
