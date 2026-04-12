using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeCatalogServiceTests
    {
        private readonly Mock<IProductService> _productService;

        public BackofficeCatalogServiceTests()
        {
            _productService = new Mock<IProductService>();
        }

        private IBackofficeCatalogService CreateSut() => new BackofficeCatalogService(_productService.Object);

        // ── GetProductsAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetProductsAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            var source = new ProductListVm
            {
                Products = new List<ProductForListVm>
                {
                    new() { Id = 1, Name = "Widget", Cost = 9.99m,  Status = "Published",   CategoryId = 10 },
                    new() { Id = 2, Name = "Gadget", Cost = 24.99m, Status = "Unpublished",  CategoryId = 20 }
                },
                CurrentPage = 1,
                PageSize = 10,
                Count = 2,
                SearchString = "get"
            };
            _productService
                .Setup(s => s.GetAllProducts(10, 1, "get"))
                .ReturnsAsync(source);

            // Act
            var result = await CreateSut().GetProductsAsync(10, 1, "get");

            // Assert
            result.Should().NotBeNull();
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.SearchString.Should().Be("get");
            result.Products.Should().HaveCount(2);

            result.Products[0].Id.Should().Be(1);
            result.Products[0].Name.Should().Be("Widget");
            result.Products[0].Cost.Should().Be(9.99m);
            result.Products[0].Status.Should().Be("Published");

            result.Products[1].Id.Should().Be(2);
            result.Products[1].Name.Should().Be("Gadget");
            result.Products[1].Status.Should().Be("Unpublished");
        }

        [Fact]
        public async Task GetProductsAsync_CategoryNameIsEmptyForAllItems()
        {
            // Arrange
            _productService
                .Setup(s => s.GetAllProducts(5, 1, string.Empty))
                .ReturnsAsync(new ProductListVm
                {
                    Products = new List<ProductForListVm>
                    {
                        new() { Id = 3, Name = "Donut", Cost = 1m, Status = "Published", CategoryId = 5 }
                    },
                    CurrentPage = 1,
                    PageSize = 5,
                    Count = 1
                });

            // Act
            var result = await CreateSut().GetProductsAsync(5, 1, null);

            // Assert — CategoryName not available in ProductForListVm; always empty
            result.Products[0].CategoryName.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProductsAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _productService
                .Setup(s => s.GetAllProducts(10, 1, string.Empty))
                .ReturnsAsync(new ProductListVm { Products = new List<ProductForListVm>(), Count = 0 });

            // Act
            var result = await CreateSut().GetProductsAsync(10, 1, null);

            // Assert
            result.Products.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task GetProductsAsync_NullSearchString_PassesEmptyStringToService()
        {
            // Arrange
            _productService
                .Setup(s => s.GetAllProducts(10, 1, string.Empty))
                .ReturnsAsync(new ProductListVm { Products = new List<ProductForListVm>() });

            // Act
            await CreateSut().GetProductsAsync(10, 1, null);

            // Assert
            _productService.Verify(s => s.GetAllProducts(10, 1, string.Empty), Times.Once);
        }

        // ── GetProductDetailAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetProductDetailAsync_ExistingProduct_ReturnsMappedVm()
        {
            // Arrange
            var detail = new ProductDetailsVm
            {
                Id = 7,
                Name = "Super Widget",
                Cost = 49.99m,
                Status = "Published",
                CategoryId = 10,
                CategoryName = "Electronics"
            };
            _productService.Setup(s => s.ProductExists(7)).ReturnsAsync(true);
            _productService.Setup(s => s.GetProductDetails(7, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

            // Act
            var result = await CreateSut().GetProductDetailAsync(7);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(7);
            result.Name.Should().Be("Super Widget");
            result.Cost.Should().Be(49.99m);
            result.Status.Should().Be("Published");
            result.CategoryId.Should().Be(10);
            result.CategoryName.Should().Be("Electronics");
        }

        [Fact]
        public async Task GetProductDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _productService.Setup(s => s.ProductExists(99)).ReturnsAsync(false);

            // Act
            var result = await CreateSut().GetProductDetailAsync(99);

            // Assert
            result.Should().BeNull();
            _productService.Verify(s => s.GetProductDetails(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
