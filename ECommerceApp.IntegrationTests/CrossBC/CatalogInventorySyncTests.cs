using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    /// <summary>
    /// Verifies Catalog → Inventory product snapshot synchronization:
    /// <list type="bullet">
    ///   <item><see cref="ProductPublished"/> → <c>ProductPublishedHandler</c> creates
    ///         <see cref="ProductSnapshot"/> in Inventory BC</item>
    /// </list>
    /// </summary>
    public class CatalogInventorySyncTests : BcBaseTest<IMessageBroker>
    {
        public CatalogInventorySyncTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task ProductPublished_ShouldCreateProductSnapshotInInventoryBc()
        {
            var msg = new ProductPublished(
                ProductId: 42,
                ProductName: "Widget",
                IsDigital: false,
                OccurredAt: DateTime.UtcNow);

            await PublishAsync(msg, CancellationToken);

            var repo = GetRequiredService<IProductSnapshotRepository>();
            var snapshot = await repo.GetByProductIdAsync(42, CancellationToken);
            snapshot.ShouldNotBeNull();
            snapshot.ProductId.ShouldBe(42);
        }

        [Fact]
        public async Task ProductPublished_DigitalProduct_ShouldCreateDigitalSnapshot()
        {
            var msg = new ProductPublished(
                ProductId: 43,
                ProductName: "E-Book",
                IsDigital: true,
                OccurredAt: DateTime.UtcNow);

            await PublishAsync(msg, CancellationToken);

            var repo = GetRequiredService<IProductSnapshotRepository>();
            var snapshot = await repo.GetByProductIdAsync(43, CancellationToken);
            snapshot.ShouldNotBeNull();
            snapshot.IsDigital.ShouldBeTrue();
        }
    }
}

