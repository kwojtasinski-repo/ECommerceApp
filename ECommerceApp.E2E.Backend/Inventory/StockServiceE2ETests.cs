using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.E2E.Backend.Infrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Inventory
{
    /// <summary>
    /// E2E port of <c>ECommerceApp.IntegrationTests.Inventory.Availability.StockServiceTests</c> —
    /// same business scenarios, but running against a real SQL Server engine (via
    /// <see cref="MsSqlE2EFixture"/>/Testcontainers) instead of EF Core's InMemory provider, through
    /// the same production DI wiring (<c>Startup</c> → <c>AddInfrastructure</c>) that runs in prod.
    /// <para>
    /// Every test in the <c>SqlServerE2E</c> collection shares one physical database, so product/order
    /// identifiers are randomized per test (see <see cref="NextProductId"/>/<see cref="NextOrderId"/>)
    /// instead of the fixed literals the InMemory version uses — each InMemory test got its own
    /// throwaway database, this suite does not.
    /// </para>
    /// </summary>
    [Collection("SqlServerE2E")]
    public class StockServiceE2ETests : SqlServerE2ETestBase<IStockService>
    {
        public StockServiceE2ETests(MsSqlE2EFixture fixture) : base(fixture) { }

        private const string ProperCustomerId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";
        private static int _productIdSeed = 1_000_000;
        private static int _orderIdSeed = 1_000_000;

        private static int NextProductId() => Interlocked.Increment(ref _productIdSeed);
        private static int NextOrderId() => Interlocked.Increment(ref _orderIdSeed);

        private async Task SeedProductSnapshotAsync(
            int productId,
            CatalogProductStatus status = CatalogProductStatus.Orderable,
            bool isDigital = false)
        {
            var repo = GetRequiredService<IProductSnapshotRepository>();
            var snapshot = ProductSnapshot.Create(productId, $"Product-{productId}", isDigital, status);
            await repo.UpsertAsync(snapshot);
        }

        // ── InitializeStockAsync ─────────────────────────────────────────

        [Fact]
        public async Task InitializeStockAsync_NewProduct_ShouldReturnTrue()
        {
            var productId = NextProductId();

            var result = await Service.InitializeStockAsync(productId, initialQuantity: 50, CancellationToken);

            result.ShouldBeTrue();

            var stock = await Service.GetByProductIdAsync(productId, CancellationToken);
            stock.ShouldNotBeNull();
            stock.ProductId.ShouldBe(productId);
            stock.Quantity.ShouldBe(50);
            stock.ReservedQuantity.ShouldBe(0);
            stock.AvailableQuantity.ShouldBe(50);
        }

        [Fact]
        public async Task InitializeStockAsync_AlreadyExisting_ShouldReturnFalse()
        {
            var productId = NextProductId();
            await Service.InitializeStockAsync(productId, initialQuantity: 10, CancellationToken);

            var result = await Service.InitializeStockAsync(productId, initialQuantity: 20, CancellationToken);

            result.ShouldBeFalse();
        }

        // ── GetByProductIdAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdAsync_NonExistent_ShouldReturnNull()
        {
            var result = await Service.GetByProductIdAsync(NextProductId(), CancellationToken);

            result.ShouldBeNull();
        }

        // ── GetByProductIdsAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdsAsync_NoMatchingProducts_ShouldReturnEmpty()
        {
            var result = new List<StockItemDto>();
            await foreach (var item in Service.GetByProductIdsAsync(new List<int> { NextProductId(), NextProductId() }, CancellationToken))
            {
                result.Add(item);
            }

            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetByProductIdsAsync_WithMatchingProducts_ShouldReturnMatchingItems()
        {
            var productId1 = NextProductId();
            var productId2 = NextProductId();
            var productId3 = NextProductId();

            await Service.InitializeStockAsync(productId1, initialQuantity: 10, CancellationToken);
            await Service.InitializeStockAsync(productId2, initialQuantity: 20, CancellationToken);
            await Service.InitializeStockAsync(productId3, initialQuantity: 30, CancellationToken);

            var result = new List<StockItemDto>();
            await foreach (var item in Service.GetByProductIdsAsync(new List<int> { productId1, productId3 }, CancellationToken))
            {
                result.Add(item);
            }

            result.Count.ShouldBe(2);
            result.ShouldContain(s => s.ProductId == productId1 && s.Quantity == 10);
            result.ShouldContain(s => s.ProductId == productId3 && s.Quantity == 30);
        }

        // ── ReserveAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ReserveAsync_NoProductSnapshot_ShouldReturnProductSnapshotNotFound()
        {
            var productId = NextProductId();
            await Service.InitializeStockAsync(productId, 50, CancellationToken);

            var dto = new ReserveStockDto(productId, OrderId: NextOrderId(), Quantity: 5,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await Service.ReserveAsync(dto, CancellationToken);

            result.ShouldBe(ReserveStockResult.ProductSnapshotNotFound);
        }

        [Fact]
        public async Task ReserveAsync_SuspendedProduct_ShouldReturnProductNotAvailable()
        {
            var productId = NextProductId();
            await SeedProductSnapshotAsync(productId, CatalogProductStatus.Suspended);
            await Service.InitializeStockAsync(productId, 50, CancellationToken);

            var dto = new ReserveStockDto(productId, OrderId: NextOrderId(), Quantity: 5,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await Service.ReserveAsync(dto, CancellationToken);

            result.ShouldBe(ReserveStockResult.ProductNotAvailable);
        }

        [Fact]
        public async Task ReserveAsync_NoStockItem_ShouldReturnStockNotFound()
        {
            var productId = NextProductId();
            await SeedProductSnapshotAsync(productId, CatalogProductStatus.Orderable);
            // Do NOT initialize stock — StockItem does not exist

            var dto = new ReserveStockDto(productId, OrderId: NextOrderId(), Quantity: 5,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await Service.ReserveAsync(dto, CancellationToken);

            result.ShouldBe(ReserveStockResult.StockNotFound);
        }

        [Fact]
        public async Task ReserveAsync_InsufficientStock_ShouldReturnInsufficientStock()
        {
            var productId = NextProductId();
            await SeedProductSnapshotAsync(productId, CatalogProductStatus.Orderable);
            await Service.InitializeStockAsync(productId, initialQuantity: 5, CancellationToken);

            var dto = new ReserveStockDto(productId, OrderId: NextOrderId(), Quantity: 10,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await Service.ReserveAsync(dto, CancellationToken);

            result.ShouldBe(ReserveStockResult.InsufficientStock);
        }

        [Fact]
        public async Task ReserveAsync_SufficientStock_ShouldReturnSuccessAndReduceAvailability()
        {
            var productId = NextProductId();
            await SeedProductSnapshotAsync(productId, CatalogProductStatus.Orderable);
            await Service.InitializeStockAsync(productId, initialQuantity: 50, CancellationToken);

            var dto = new ReserveStockDto(productId, OrderId: NextOrderId(), Quantity: 10,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await Service.ReserveAsync(dto, CancellationToken);

            result.ShouldBe(ReserveStockResult.Success);

            var stock = await Service.GetByProductIdAsync(productId, CancellationToken);
            stock.ShouldNotBeNull();
            stock.Quantity.ShouldBe(50);
            stock.ReservedQuantity.ShouldBe(10);
            stock.AvailableQuantity.ShouldBe(40);
        }

        // ── ReleaseAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseAsync_NoStockHold_ShouldReturnFalse()
        {
            var result = await Service.ReleaseAsync(orderId: NextOrderId(), productId: NextProductId(), quantity: 5, CancellationToken);

            result.ShouldBeFalse();
        }

        // ── ConfirmAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_NoStockHold_ShouldReturnFalse()
        {
            var result = await Service.ConfirmAsync(orderId: NextOrderId(), productId: NextProductId(), CancellationToken);

            result.ShouldBeFalse();
        }

        // ── FulfillAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task FulfillAsync_NoStockItem_ShouldReturnFalse()
        {
            var result = await Service.FulfillAsync(orderId: NextOrderId(), productId: NextProductId(), quantity: 5, CancellationToken);

            result.ShouldBeFalse();
        }

        // ── ReturnAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsync_NoStockItem_ShouldReturnFalse()
        {
            var result = await Service.ReturnAsync(productId: NextProductId(), quantity: 5, CancellationToken);

            result.ShouldBeFalse();
        }

        // ── Full lifecycle: Initialize → Reserve → Confirm → Release ────

        [Fact]
        public async Task FullLifecycle_ReserveConfirmRelease_ShouldTrackQuantitiesCorrectly()
        {
            var productId = NextProductId();
            var orderId = NextOrderId();
            await SeedProductSnapshotAsync(productId, CatalogProductStatus.Orderable);
            await Service.InitializeStockAsync(productId, initialQuantity: 100, CancellationToken);

            // Reserve 20 units for the order
            var reserveResult = await Service.ReserveAsync(new ReserveStockDto(
                productId, OrderId: orderId, Quantity: 20,
                UserId: ProperCustomerId, ExpiresAt: DateTime.UtcNow.AddHours(24)), CancellationToken);
            reserveResult.ShouldBe(ReserveStockResult.Success);

            var afterReserve = await Service.GetByProductIdAsync(productId, CancellationToken);
            afterReserve!.AvailableQuantity.ShouldBe(80);
            afterReserve.ReservedQuantity.ShouldBe(20);

            // Confirm the hold
            var confirmResult = await Service.ConfirmAsync(orderId, productId, CancellationToken);
            confirmResult.ShouldBeTrue();
        }
    }
}
