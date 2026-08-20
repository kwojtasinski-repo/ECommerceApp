using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Web.E2E.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class OutboxDispatchWatcherTests
    {
        [Fact]
        public async Task WaitForDispatchedAsync_ObservesProductPublishedAndTimesOutForMissingMessage()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            using var scope = services.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var categoryRepository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var categoryId = (await categoryRepository.AddAsync(Category.Create("E2E category"))).Value;
            var watcher = new OutboxDispatchWatcher(
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                TimeSpan.FromMilliseconds(50));
            var sinceUtc = DateTime.UtcNow;

            var productId = await productService.AddProduct(new CreateProductDto(
                Name: "E2E product",
                Cost: 9.99m,
                Description: "Outbox watcher test",
                CategoryId: categoryId,
                TagIds: Array.Empty<int>()));
            await productService.PublishProduct(productId);

            await watcher.WaitForDispatchedAsync<ProductPublished>(
                sinceUtc,
                message => message.ProductId == productId,
                ct: TestContext.Current.CancellationToken);

            await Should.ThrowAsync<TimeoutException>(() => watcher.WaitForDispatchedAsync<ProductPublished>(
                DateTime.UtcNow,
                message => message.ProductId == -1,
                TimeSpan.FromMilliseconds(250),
                TestContext.Current.CancellationToken));
        }
    }
}
