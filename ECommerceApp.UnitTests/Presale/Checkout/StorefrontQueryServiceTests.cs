using ECommerceApp.Application.Constants;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class StorefrontQueryServiceTests
    {
        private readonly Mock<ICatalogClient> _catalog;
        private readonly Mock<IStockSnapshotRepository> _stockSnapshots;
        private readonly StorefrontQueryService _service;

        public StorefrontQueryServiceTests()
        {
            _catalog = new Mock<ICatalogClient>();
            _stockSnapshots = new Mock<IStockSnapshotRepository>();
            _service = new StorefrontQueryService(_catalog.Object, _stockSnapshots.Object, new Mock<IMemoryCache>().Object, Options.Create(new CacheOptions()));
        }

        [Fact]
        public async Task GetPublishedProductsAsync_MergesStockAvailability()
        {
            // Arrange
            SetupPublishedProducts(ProductPageWith(new CatalogProductItem(5, "Bag", 49.99m, 2, null)));
            SetupStockSnapshots(StockSnapshot.Create(5, 7, DateTime.UtcNow));

            // Act
            var result = await _service.GetPublishedProductsAsync(10, 1, "", null, TestContext.Current.CancellationToken);

            // Assert
            result.Products.Should().ContainSingle(i =>
                i.ProductId == 5 &&
                i.Name == "Bag" &&
                i.Price == 49.99m &&
                i.AvailableQuantity == 7 &&
                i.InStock);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_NoStockEntry_ReturnsAvailableZeroAndInStockFalse()
        {
            // Arrange
            SetupPublishedProducts(ProductPageWith(new CatalogProductItem(3, "Hat", 20m, 1, null)));
            SetupStockSnapshots();

            // Act
            var result = await _service.GetPublishedProductsAsync(10, 1, "", null, TestContext.Current.CancellationToken);

            // Assert
            result.Products.Should().ContainSingle(i =>
                i.ProductId == 3 &&
                i.AvailableQuantity == 0 &&
                !i.InStock);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_EmptyProductList_ReturnsEmptyItems()
        {
            // Arrange
            SetupPublishedProducts(new CatalogProductPage(new List<CatalogProductItem>(), 0, 10, 1, ""));
            SetupStockSnapshots();

            // Act
            var result = await _service.GetPublishedProductsAsync(10, 1, "", null, TestContext.Current.CancellationToken);

            // Assert
            result.Products.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            _stockSnapshots.Verify(s => s.GetByProductIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_PaginationMetadataPassedThrough()
        {
            // Arrange
            SetupPublishedProducts(new CatalogProductPage(new List<CatalogProductItem>(), 42, 5, 2, "coat"), 5, 2, "coat");
            SetupStockSnapshots();

            // Act
            var result = await _service.GetPublishedProductsAsync(5, 2, "coat", null, TestContext.Current.CancellationToken);

            // Assert
            result.TotalCount.Should().Be(42);
            result.PageSize.Should().Be(5);
            result.CurrentPage.Should().Be(2);
            result.SearchString.Should().Be("coat");
        }

        [Fact]
        public async Task GetPublishedProductsAsync_MultipleProducts_PassesAllIdsInSingleBatchCall()
        {
            // Arrange
            var p1 = new CatalogProductItem(1, "A", 10m, 1, null);
            var p2 = new CatalogProductItem(2, "B", 20m, 1, null);
            SetupPublishedProducts(ProductPageWith(p1, p2));
            SetupStockSnapshots(
                StockSnapshot.Create(1, 5, DateTime.UtcNow),
                StockSnapshot.Create(2, 2, DateTime.UtcNow));

            // Act
            var result = await _service.GetPublishedProductsAsync(10, 1, "", null, TestContext.Current.CancellationToken);

            // Assert
            result.Products.Should().HaveCount(2);
            // All product IDs collected and passed in one batch — not N separate calls
            _stockSnapshots.Verify(s => s.GetByProductIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // �� helpers ����������������������������������������������������������

        private static async IAsyncEnumerable<StockSnapshot> AsAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default,
            params StockSnapshot[] items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }

        private static IAsyncEnumerable<StockSnapshot> AsAsyncEnumerable(params StockSnapshot[] items)
            => AsAsyncEnumerable(default, items);

        private void SetupPublishedProducts(CatalogProductPage page, int pageSize = 10, int currentPage = 1, string search = "")
        {
            _catalog.Setup(p => p.GetPublishedProductsAsync(pageSize, currentPage, search, It.IsAny<CancellationToken>()))
                .ReturnsAsync(page);
        }

        private void SetupStockSnapshots(params StockSnapshot[] snapshots)
        {
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable(snapshots));
        }

        private static CatalogProductPage ProductPageWith(params CatalogProductItem[] items) =>
            new CatalogProductPage(
                new List<CatalogProductItem>(items),
                items.Length,
                10,
                1,
                "");
    }
}
