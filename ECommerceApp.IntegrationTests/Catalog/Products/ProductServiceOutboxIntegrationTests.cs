using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Catalog.Products
{
    public class ProductServiceOutboxIntegrationTests : BcBaseTest<IProductService>
    {
        public ProductServiceOutboxIntegrationTests(ITestOutputHelper output) : base(output) { }

        private async Task<int> SeedCategoryAsync(string name = "Elektronika")
        {
            var repo = GetRequiredService<ICategoryRepository>();
            var category = Category.Create(name);
            var id = await repo.AddAsync(category);
            return id.Value;
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        [Fact]
        public async Task PublishProduct_EnqueuesOutboxMessage_AndInventorySnapshotEventuallyCreated()
        {
            var categoryId = await SeedCategoryAsync();

            var id = await _service.AddProduct(new CreateProductDto(
                Name: "Widget",
                Cost: 9.99m,
                Description: "Test",
                CategoryId: categoryId,
                TagIds: Array.Empty<int>()));

            await _service.PublishProduct(id);

            var repo = GetRequiredService<IProductSnapshotRepository>();

            await WaitUntilAsync(async () =>
            {
                var snapshot = await repo.GetByProductIdAsync(id, CancellationToken);
                return snapshot is not null;
            }, TimeSpan.FromSeconds(20));

            var final = await repo.GetByProductIdAsync(id, CancellationToken);
            final.ShouldNotBeNull();
            final.ProductId.ShouldBe(id);
        }
    }
}
