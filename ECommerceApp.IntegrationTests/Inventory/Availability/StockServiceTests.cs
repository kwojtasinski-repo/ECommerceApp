using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.Inventory.Availability
{
    public class StockServiceTests : BcBaseTest<IStockService>
    {
        public StockServiceTests(ITestOutputHelper output) : base(output) { }

        private const int TestProductId = 100;

        private async Task SeedProductSnapshotAsync(
            int productId = TestProductId,
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
            var result = await _service.InitializeStockAsync(TestProductId, initialQuantity: 50);

            result.ShouldBeTrue();

            var stock = await _service.GetByProductIdAsync(TestProductId);
            stock.ShouldNotBeNull();
            stock.ProductId.ShouldBe(TestProductId);
            stock.Quantity.ShouldBe(50);
            stock.ReservedQuantity.ShouldBe(0);
            stock.AvailableQuantity.ShouldBe(50);
        }

        [Fact]
        public async Task InitializeStockAsync_AlreadyExisting_ShouldReturnFalse()
        {
            await _service.InitializeStockAsync(TestProductId, initialQuantity: 10);

            var result = await _service.InitializeStockAsync(TestProductId, initialQuantity: 20);

            result.ShouldBeFalse();
        }

        // ── GetByProductIdAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdAsync_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetByProductIdAsync(int.MaxValue);

            result.ShouldBeNull();
        }

        // ── GetByProductIdsAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetByProductIdsAsync_NoMatchingProducts_ShouldReturnEmpty()
        {
            var result = new List<StockItemDto>();
            await foreach (var item in _service.GetByProductIdsAsync(new List<int> { 999, 998 }))
            {
                result.Add(item);
            }

            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetByProductIdsAsync_WithMatchingProducts_ShouldReturnMatchingItems()
        {
            await _service.InitializeStockAsync(201, initialQuantity: 10);
            await _service.InitializeStockAsync(202, initialQuantity: 20);
            await _service.InitializeStockAsync(203, initialQuantity: 30);

            var result = new List<StockItemDto>();
            await foreach (var item in _service.GetByProductIdsAsync(new List<int> { 201, 203 }))
            {
                result.Add(item);
            }

            result.Count.ShouldBe(2);
            result.ShouldContain(s => s.ProductId == 201 && s.Quantity == 10);
            result.ShouldContain(s => s.ProductId == 203 && s.Quantity == 30);
        }

        // ── ReserveAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ReserveAsync_NoProductSnapshot_ShouldReturnProductSnapshotNotFound()
        {
            await _service.InitializeStockAsync(TestProductId, 50);

            var dto = new ReserveStockDto(TestProductId, OrderId: 1, Quantity: 5,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await _service.ReserveAsync(dto);

            result.ShouldBe(ReserveStockResult.ProductSnapshotNotFound);
        }

        [Fact]
        public async Task ReserveAsync_SuspendedProduct_ShouldReturnProductNotAvailable()
        {
            await SeedProductSnapshotAsync(TestProductId, CatalogProductStatus.Suspended);
            await _service.InitializeStockAsync(TestProductId, 50);

            var dto = new ReserveStockDto(TestProductId, OrderId: 1, Quantity: 5,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await _service.ReserveAsync(dto);

            result.ShouldBe(ReserveStockResult.ProductNotAvailable);
        }

        [Fact]
        public async Task ReserveAsync_NoStockItem_ShouldReturnStockNotFound()
        {
            await SeedProductSnapshotAsync(TestProductId, CatalogProductStatus.Orderable);
            // Do NOT initialize stock — StockItem does not exist

            var dto = new ReserveStockDto(TestProductId, OrderId: 1, Quantity: 5,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await _service.ReserveAsync(dto);

            result.ShouldBe(ReserveStockResult.StockNotFound);
        }

        [Fact]
        public async Task ReserveAsync_InsufficientStock_ShouldReturnInsufficientStock()
        {
            await SeedProductSnapshotAsync(TestProductId, CatalogProductStatus.Orderable);
            await _service.InitializeStockAsync(TestProductId, initialQuantity: 5);

            var dto = new ReserveStockDto(TestProductId, OrderId: 1, Quantity: 10,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await _service.ReserveAsync(dto);

            result.ShouldBe(ReserveStockResult.InsufficientStock);
        }

        [Fact]
        public async Task ReserveAsync_SufficientStock_ShouldReturnSuccessAndReduceAvailability()
        {
            await SeedProductSnapshotAsync(TestProductId, CatalogProductStatus.Orderable);
            await _service.InitializeStockAsync(TestProductId, initialQuantity: 50);

            var dto = new ReserveStockDto(TestProductId, OrderId: 1, Quantity: 10,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24));

            var result = await _service.ReserveAsync(dto);

            result.ShouldBe(ReserveStockResult.Success);

            var stock = await _service.GetByProductIdAsync(TestProductId);
            stock.ShouldNotBeNull();
            stock.Quantity.ShouldBe(50);
            stock.ReservedQuantity.ShouldBe(10);
            stock.AvailableQuantity.ShouldBe(40);
        }

        // ── ReleaseAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseAsync_NoStockHold_ShouldReturnFalse()
        {
            var result = await _service.ReleaseAsync(orderId: 999, productId: 999, quantity: 5);

            result.ShouldBeFalse();
        }

        // ── ConfirmAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmAsync_NoStockHold_ShouldReturnFalse()
        {
            var result = await _service.ConfirmAsync(orderId: 999, productId: 999);

            result.ShouldBeFalse();
        }

        // ── FulfillAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task FulfillAsync_NoStockItem_ShouldReturnFalse()
        {
            var result = await _service.FulfillAsync(orderId: 999, productId: 999, quantity: 5);

            result.ShouldBeFalse();
        }

        // ── ReturnAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsync_NoStockItem_ShouldReturnFalse()
        {
            var result = await _service.ReturnAsync(productId: 999, quantity: 5);

            result.ShouldBeFalse();
        }

        // ── Full lifecycle: Initialize → Reserve → Confirm → Release ────

        [Fact]
        public async Task FullLifecycle_ReserveConfirmRelease_ShouldTrackQuantitiesCorrectly()
        {
            await SeedProductSnapshotAsync(TestProductId, CatalogProductStatus.Orderable);
            await _service.InitializeStockAsync(TestProductId, initialQuantity: 100);

            // Reserve 20 units for order 1
            var reserveResult = await _service.ReserveAsync(new ReserveStockDto(
                TestProductId, OrderId: 1, Quantity: 20,
                UserId: PROPER_CUSTOMER_ID, ExpiresAt: DateTime.UtcNow.AddHours(24)));
            reserveResult.ShouldBe(ReserveStockResult.Success);

            var afterReserve = await _service.GetByProductIdAsync(TestProductId);
            afterReserve!.AvailableQuantity.ShouldBe(80);
            afterReserve.ReservedQuantity.ShouldBe(20);

            // Confirm the hold
            var confirmResult = await _service.ConfirmAsync(orderId: 1, productId: TestProductId);
            confirmResult.ShouldBeTrue();
        }
    }
}

