using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Products
{
    public class ProductServiceTests
    {
        private readonly Mock<IProductRepository> _productRepo = new();
        private readonly Mock<ICategoryRepository> _categoryRepo = new();
        private readonly Mock<IProductTagRepository> _tagRepo = new();
        private readonly Mock<IImageUrlBuilder> _urlBuilder = new();
        private readonly Mock<ICatalogUnitOfWork> _uow = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly IOptions<CacheOptions> _cacheOptions = Options.Create(new CacheOptions());

        private ProductService CreateService()
        {
            return new ProductService(
                _productRepo.Object,
                _categoryRepo.Object,
                _tagRepo.Object,
                _urlBuilder.Object,
                _uow.Object,
                _outboxWriter.Object,
                _cache,
                _cacheOptions);
        }

        private Mock<IOutboxTransaction> SetupProductPublishing()
        {
            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _productRepo.Setup(r => r.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
            return txMock;
        }

        [Fact]
        public async Task UpdateProduct_EnqueuesAndCommits()
        {
            var dto = new UpdateProductDto(1, "UpdatedName", 1m, "d", 1, System.Array.Empty<int>());
            var product = Product.Create("OriginalName", 1m, "d", 1);
            EntityIdSetter.Set(product, new ProductId(1));
            _productRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<ProductId>())).ReturnsAsync(product);
            _categoryRepo.Setup(r => r.ExistsByIdAsync(It.IsAny<CategoryId>())).ReturnsAsync(true);

            var txMock = SetupProductPublishing();

            var svc = CreateService();
            var result = await svc.UpdateProduct(dto);

            result.Should().BeTrue();
            _productRepo.Verify(r => r.UpdateAsync(product), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), txMock.Object, It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishProduct_EnqueuesAndCommits()
        {
            var product = Product.Create("ProdPublish", 1m, "d", 1);
            EntityIdSetter.Set(product, new ProductId(1));
            _productRepo.Setup(r => r.GetByIdAsync(It.IsAny<ProductId>())).ReturnsAsync(product);

            var txMock = SetupProductPublishing();

            var svc = CreateService();
            await svc.PublishProduct(1);

            _productRepo.Verify(r => r.UpdateAsync(product), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), txMock.Object, It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnpublishProduct_EnqueuesAndCommits()
        {
            var product = Product.Create("ProdUnpublish", 1m, "d", 1);
            EntityIdSetter.Set(product, new ProductId(1));
            // ensure product is published before unpublish
            product.Publish();
            _productRepo.Setup(r => r.GetByIdAsync(It.IsAny<ProductId>())).ReturnsAsync(product);

            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _productRepo.Setup(r => r.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);

            var svc = CreateService();
            await svc.UnpublishProduct(1);

            _productRepo.Verify(r => r.UpdateAsync(product), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.IsAny<IMessage>(), txMock.Object, It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
