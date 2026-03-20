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
    public class ProductPublishedHandlerTests
    {
        private readonly Mock<IProductSnapshotRepository> _snapshotRepo;
        private readonly ProductPublishedHandler _handler;

        public ProductPublishedHandlerTests()
        {
            _snapshotRepo = new Mock<IProductSnapshotRepository>();
            _handler = new ProductPublishedHandler(_snapshotRepo.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldUpsertSnapshotWithOrderableStatus()
        {
            var message = new ProductPublished(
                ProductId: 42,
                ProductName: "Widget",
                IsDigital: false,
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.Is<ProductSnapshot>(s =>
                    s.ProductId == 42 &&
                    s.ProductName == "Widget" &&
                    s.IsDigital == false &&
                    s.CatalogStatus == CatalogProductStatus.Orderable),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_DigitalProduct_ShouldSetIsDigitalTrue()
        {
            var message = new ProductPublished(
                ProductId: 10,
                ProductName: "E-Book",
                IsDigital: true,
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _snapshotRepo.Verify(r => r.UpsertAsync(
                It.Is<ProductSnapshot>(s =>
                    s.ProductId == 10 &&
                    s.IsDigital == true &&
                    s.CatalogStatus == CatalogProductStatus.Orderable),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
