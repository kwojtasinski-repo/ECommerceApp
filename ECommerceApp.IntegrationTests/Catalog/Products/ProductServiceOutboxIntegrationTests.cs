using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Catalog.Products
{
    public class ProductServiceOutboxIntegrationTests
        : BcBaseTest<IProductService>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public ProductServiceOutboxIntegrationTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

        private async Task<int> SeedCategoryAsync(string name = "Elektronika")
        {
            var repo = GetRequiredService<ICategoryRepository>();
            var category = Category.Create(name);
            var id = await repo.AddAsync(category);
            return id.Value;
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

            var final = await _messageProcessing.WaitUntilAsync(
                new ProductSnapshotCreatedOperation(repo, id),
                cancellationToken: CancellationToken);

            final.ShouldNotBeNull();
            final.ProductId.ShouldBe(id);
        }

        private sealed class ProductSnapshotCreatedOperation : IMessageProcessingOperation<ProductSnapshot>
        {
            private readonly IProductSnapshotRepository _repository;
            private readonly int _productId;

            public ProductSnapshotCreatedOperation(
                IProductSnapshotRepository repository,
                int productId)
            {
                _repository = repository;
                _productId = productId;
            }

            public Task<ProductSnapshot> ReadAsync(CancellationToken cancellationToken)
            {
                return _repository.GetByProductIdAsync(_productId, cancellationToken);
            }

            public bool IsCompleted(ProductSnapshot state)
            {
                return state is not null;
            }

            public string Describe(ProductSnapshot state)
            {
                return state is null
                    ? $"Inventory snapshot for product {_productId} was not created before the timeout."
                    : $"Inventory snapshot for product {_productId} was created.";
            }
        }
    }
}
