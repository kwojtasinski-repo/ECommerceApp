using AwesomeAssertions;
using ECommerceApp.Application.Catalog.Images;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Products;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Constants;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Images
{
    /// <summary>
    /// ProductService caches the product (gallery included) for CatalogProductTtl. Changing the gallery through
    /// ImageService must evict that entry, otherwise the page shows the old gallery after an upload or delete.
    /// </summary>
    public class ImageServiceProductCacheTests
    {
        private const int ProductId = 7;
        private const int OtherProductId = 8;

        private readonly Mock<IImageRepository> _imageRepository = new();
        private readonly Mock<IFileStoreProvider> _fileStoreProvider = new();
        private readonly Mock<IProductRepository> _productRepository = new();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

        [Fact]
        public async Task AddImages_ProductDetailsCached_NextGetProductDetailsReloadsGallery()
        {
            var product = CreateProduct(ProductId);
            SetupProductsInRepository(product);
            SetupImageStorage();
            var productService = CreateProductService();
            var imageService = CreateImageService();
            (await productService.GetProductDetails(ProductId)).Images.Should().BeEmpty();

            await imageService.AddImages(new AddImagesPOCO { ItemId = ProductId, Files = new List<IFormFile> { CreateJpeg() } });

            (await productService.GetProductDetails(ProductId)).Images.Should().HaveCount(1);
        }

        [Fact]
        public async Task Add_ProductDetailsCached_NextGetProductDetailsReloadsGallery()
        {
            var product = CreateProduct(ProductId);
            SetupProductsInRepository(product);
            SetupImageStorage();
            var productService = CreateProductService();
            var imageService = CreateImageService();
            (await productService.GetProductDetails(ProductId)).Images.Should().BeEmpty();

            await imageService.Add(new ImageVm { ItemId = ProductId, Images = new List<IFormFile> { CreateJpeg() } });

            (await productService.GetProductDetails(ProductId)).Images.Should().HaveCount(1);
        }

        [Fact]
        public async Task Delete_ProductDetailsCached_NextGetProductDetailsReloadsGallery()
        {
            var product = CreateProduct(ProductId);
            product.AddImage("a.jpg", "/upload", "Local", imageId: 41);
            SetupProductsInRepository(product);
            _imageRepository.Setup(r => r.GetImageById(41)).ReturnsAsync(product.Images.Single());
            var productService = CreateProductService();
            var imageService = CreateImageService();
            (await productService.GetProductDetails(ProductId)).Images.Should().HaveCount(1);

            (await imageService.Delete(41)).Should().BeTrue();

            (await productService.GetProductDetails(ProductId)).Images.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_OtherProductCached_KeepsOtherProductCacheEntry()
        {
            var product = CreateProduct(ProductId);
            var other = CreateProduct(OtherProductId);
            SetupProductsInRepository(product, other);
            SetupImageStorage();
            var productService = CreateProductService();
            var imageService = CreateImageService();
            var cachedOther = await productService.GetProductDetails(OtherProductId);

            await imageService.AddImages(new AddImagesPOCO { ItemId = ProductId, Files = new List<IFormFile> { CreateJpeg() } });

            (await productService.GetProductDetails(OtherProductId)).Should().BeSameAs(cachedOther);
        }

        private ProductService CreateProductService()
        {
            var urlBuilder = new Mock<IImageUrlBuilder>();
            urlBuilder.Setup(b => b.Build(It.IsAny<int>())).Returns("/img");
            return new ProductService(
                _productRepository.Object,
                new Mock<ICategoryRepository>().Object,
                new Mock<IProductTagRepository>().Object,
                urlBuilder.Object,
                new Mock<ICatalogUnitOfWork>().Object,
                new Mock<IOutboxWriter>().Object,
                _cache,
                Options.Create(new CacheOptions()));
        }

        private ImageService CreateImageService()
            => new ImageService(
                _imageRepository.Object,
                _fileStoreProvider.Object,
                _productRepository.Object,
                new ImageSanitizer(Options.Create(new ImageLimitsOptions())),
                new ProductCacheInvalidator(_cache));

        private void SetupProductsInRepository(params Product[] products)
        {
            foreach (var product in products)
            {
                var current = product;
                _productRepository.Setup(r => r.GetByIdWithDetailsAsync(current.Id, It.IsAny<CancellationToken>())).ReturnsAsync(current);
            }

            // Stands in for EF Core handing out ids when the product is saved.
            _productRepository.Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Callback<Product>(AssignImageIds)
                .Returns(Task.CompletedTask);
        }

        private void SetupImageStorage()
        {
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            _fileStoreProvider.Setup(p => p.WriteFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync(new FileDirectoryPOCO { Name = "Name", SourcePath = "/upload/Name" });
        }

        private static Product CreateProduct(int id)
        {
            var product = Product.Create("Product " + id, 10m, "Description", 1);
            typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, new ProductId(id));
            return product;
        }

        private static void AssignImageIds(Product product)
        {
            var nextId = 100;
            foreach (var image in product.Images.Where(i => i.Id is null))
            {
                typeof(Image).GetProperty(nameof(Image.Id))!.SetValue(image, new ImageId(nextId++));
            }
        }

        private static IFormFile CreateJpeg()
        {
            var stream = new MemoryStream(ImagePayloads.ValidJpeg());
            return new FormFile(stream, 0, stream.Length, ".jpg", "photo.jpg") { Headers = new HeaderDictionary() };
        }
    }
}
