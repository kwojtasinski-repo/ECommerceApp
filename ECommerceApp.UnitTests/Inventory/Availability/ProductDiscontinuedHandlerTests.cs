using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Domain.Inventory.Availability;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class ProductDiscontinuedHandlerTests
    {
        private readonly Mock<IProductSnapshotRepository> _snapshotRepo;
        private readonly ProductDiscontinuedHandler _handler;

        public ProductDiscontinuedHandlerTests()
        {
            _snapshotRepo = new Mock<IProductSnapshotRepository>();
            _handler = new ProductDiscontinuedHandler(_snapshotRepo.Object);
        }

        [Fact]
        public async Task HandleAsync_ExistingSnapshot_ShouldUpsertWithDiscontinuedStatus()
        {
            var existing = ProductSnapshot.Create(42, "Widget", false, CatalogProductStatus.Orderable);
            _snapshotRepo.Setup(r => r.GetByProductIdAsync(42, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(existing);

            var message = new ProductDiscontinued(ProductId: 42, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.Is<ProductSnapshot>(s =>
                    s.ProductId == 42 &&
                    s.ProductName == "Widget" &&
                    s.IsDigital == false &&
                    s.CatalogStatus == CatalogProductStatus.Discontinued),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_NoExistingSnapshot_ShouldNotUpsert()
        {
            _snapshotRepo.Setup(r => r.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
                         .ReturnsAsync((ProductSnapshot)null);

            var message = new ProductDiscontinued(ProductId: 99, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.IsAny<ProductSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
