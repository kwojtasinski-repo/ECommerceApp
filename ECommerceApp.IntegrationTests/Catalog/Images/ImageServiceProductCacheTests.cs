using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Catalog.Images
{
    /// <summary>
    /// Real DI and the real IMemoryCache: product details are cached for CatalogProductTtl, so the gallery shown
    /// after an upload or a delete must not be the stale cached one. Every call runs in its own DI scope, as
    /// separate web requests do, so only the cache (not a shared DbContext) can make a read stale.
    /// </summary>
    public class ImageServiceProductCacheTests : BcBaseTest<IImageService>
    {
        private static readonly byte[] OnePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        public ImageServiceProductCacheTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task AddImages_ProductDetailsAlreadyCached_ReturnsNewGalleryOnNextRead()
        {
            var productId = await SeedProductAsync();
            (await GetGalleryAsync(productId)).ShouldBeEmpty();

            await AddImageAsync(productId);

            (await GetGalleryAsync(productId)).Count.ShouldBe(1);
        }

        [Fact]
        public async Task Delete_ProductDetailsAlreadyCached_ReturnsGalleryWithoutDeletedImageOnNextRead()
        {
            var productId = await SeedProductAsync();
            var imageId = await AddImageAsync(productId);
            (await GetGalleryAsync(productId)).Count.ShouldBe(1);

            var deleted = await InScopeAsync(sp => sp.GetRequiredService<IImageService>().Delete(imageId));

            deleted.ShouldBeTrue();
            (await GetGalleryAsync(productId)).ShouldBeEmpty();
        }

        private Task<int> SeedProductAsync()
            => InScopeAsync(async sp =>
            {
                var categoryId = (await sp.GetRequiredService<ICategoryRepository>().AddAsync(Category.Create("Elektronika"))).Value;
                return await sp.GetRequiredService<IProductService>().AddProduct(new CreateProductDto(
                    Name: "Laptop",
                    Cost: 3999.99m,
                    Description: "Powerful laptop",
                    CategoryId: categoryId,
                    TagIds: Enumerable.Empty<int>()));
            });

        private Task<int> AddImageAsync(int productId)
            => InScopeAsync(async sp =>
            {
                var stream = new MemoryStream(OnePixelPng);
                var file = new FormFile(stream, 0, stream.Length, "file", "pixel.png") { Headers = new HeaderDictionary() };
                var ids = await sp.GetRequiredService<IImageService>().AddImages(new AddImagesPOCO { ItemId = productId, Files = new List<IFormFile> { file } });
                return ids.Single();
            });

        private Task<List<int>> GetGalleryAsync(int productId)
            => InScopeAsync(async sp =>
            {
                var details = await sp.GetRequiredService<IProductService>().GetProductDetails(productId, CancellationToken);
                return details.Images.Select(i => i.Id).ToList();
            });

        private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider);
        }
    }
}
