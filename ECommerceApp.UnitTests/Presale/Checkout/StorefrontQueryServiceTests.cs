using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using FluentAssertions;
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
        private readonly Mock<IProductService> _products;
        private readonly Mock<IStockSnapshotRepository> _stockSnapshots;
        private readonly StorefrontQueryService _service;

        public StorefrontQueryServiceTests()
        {
            _products = new Mock<IProductService>();
            _stockSnapshots = new Mock<IStockSnapshotRepository>();
            _service = new StorefrontQueryService(_products.Object, _stockSnapshots.Object);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_MergesStockAvailability()
        {
            _products.Setup(p => p.GetPublishedProducts(10, 1, ""))
                .ReturnsAsync(ProductListWith(new ProductForListVm { Id = 5, Name = "Bag", Cost = 49.99m, CategoryId = 2 }));
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable(StockSnapshot.Create(5, 7, DateTime.UtcNow)));

            var result = await _service.GetPublishedProductsAsync(10, 1, "");

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
            _products.Setup(p => p.GetPublishedProducts(10, 1, ""))
                .ReturnsAsync(ProductListWith(new ProductForListVm { Id = 3, Name = "Hat", Cost = 20m, CategoryId = 1 }));
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable());

            var result = await _service.GetPublishedProductsAsync(10, 1, "");

            result.Products.Should().ContainSingle(i =>
                i.ProductId == 3 &&
                i.AvailableQuantity == 0 &&
                !i.InStock);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_EmptyProductList_ReturnsEmptyItems()
        {
            _products.Setup(p => p.GetPublishedProducts(10, 1, ""))
                .ReturnsAsync(new ProductListVm { Products = new List<ProductForListVm>(), Count = 0, PageSize = 10, CurrentPage = 1, SearchString = "" });
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable());

            var result = await _service.GetPublishedProductsAsync(10, 1, "");

            result.Products.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            _stockSnapshots.Verify(s => s.GetByProductIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPublishedProductsAsync_PaginationMetadataPassedThrough()
        {
            _products.Setup(p => p.GetPublishedProducts(5, 2, "coat"))
                .ReturnsAsync(new ProductListVm { Products = new List<ProductForListVm>(), Count = 42, PageSize = 5, CurrentPage = 2, SearchString = "coat" });
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable());

            var result = await _service.GetPublishedProductsAsync(5, 2, "coat");

            result.TotalCount.Should().Be(42);
            result.PageSize.Should().Be(5);
            result.CurrentPage.Should().Be(2);
            result.SearchString.Should().Be("coat");
        }

        [Fact]
        public async Task GetPublishedProductsAsync_MultipleProducts_PassesAllIdsInSingleBatchCall()
        {
            var p1 = new ProductForListVm { Id = 1, Name = "A", Cost = 10m, CategoryId = 1 };
            var p2 = new ProductForListVm { Id = 2, Name = "B", Cost = 20m, CategoryId = 1 };
            _products.Setup(p => p.GetPublishedProducts(10, 1, ""))
                .ReturnsAsync(ProductListWith(p1, p2));
            _stockSnapshots.Setup(s => s.GetByProductIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .Returns(AsAsyncEnumerable(
                    StockSnapshot.Create(1, 5, DateTime.UtcNow),
                    StockSnapshot.Create(2, 2, DateTime.UtcNow)));

            var result = await _service.GetPublishedProductsAsync(10, 1, "");

            result.Products.Should().HaveCount(2);
            // All product IDs collected and passed in one batch â€” not N separate calls
            _stockSnapshots.Verify(s => s.GetByProductIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static async IAsyncEnumerable<StockSnapshot> AsAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default,
            params StockSnapshot[] items)
        {
            foreach (var item in items)
                yield return item;
        }

        private static IAsyncEnumerable<StockSnapshot> AsAsyncEnumerable(params StockSnapshot[] items)
            => AsAsyncEnumerable(default, items);

        private static ProductListVm ProductListWith(params ProductForListVm[] items) =>
            new ProductListVm
            {
                Products = new List<ProductForListVm>(items),
                Count = items.Length,
                PageSize = 10,
                CurrentPage = 1,
                SearchString = ""
            };
    }
}
