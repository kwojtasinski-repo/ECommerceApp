using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Presale.Checkout
{
    public class SoftReservationServiceTests : BcBaseTest<ISoftReservationService>
    {
        public SoftReservationServiceTests(ITestOutputHelper output) : base(output) { }

        private async Task<int> SeedProductAsync(decimal price = 99.99m, CancellationToken ct = default)
        {
            var categoryRepo = GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepo.AddAsync(Category.Create("SoftResTest Category"));

            var productService = GetRequiredService<IProductService>();
            var productId = await productService.AddProduct(new CreateProductDto(
                "SoftRes Test Product", price, "desc", categoryId.Value, new List<int>()));
            await productService.PublishProduct(productId);
            return productId;
        }

        private async Task SeedStockSnapshotAsync(int productId, int availableQty = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IStockSnapshotRepository>();
            await snapshotRepo.AddAsync(StockSnapshot.Create(productId, availableQty, DateTime.UtcNow), CancellationToken);
        }

        // ── GetAllForUserAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetAllForUserAsync_NoReservations_ReturnsEmpty()
        {
            var result = await _service.GetAllForUserAsync(new PresaleUserId("sr-user-norel"), CancellationToken);

            result.ShouldBeEmpty();
        }

        // ── HoldAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task HoldAsync_NoStockSnapshotExists_ReturnsFalse()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            // No snapshot seeded for this product

            var result = await _service.HoldAsync(productId, "sr-user-nosnap", 1, CancellationToken);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task HoldAsync_WithAvailableStockAndPrice_ReturnsTrue()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);

            var result = await _service.HoldAsync(productId, "sr-user-holdok", 2, CancellationToken);

            result.ShouldBeTrue();
        }

        [Fact]
        public async Task HoldAsync_InsufficientStock_ReturnsFalse()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, availableQty: 1, ct: CancellationToken);

            var result = await _service.HoldAsync(productId, "sr-user-nostock", quantity: 5, CancellationToken);

            result.ShouldBeFalse();
        }

        // ── GetAllForUserAsync after hold ────────────────────────────────

        [Fact]
        public async Task GetAllForUserAsync_AfterSuccessfulHold_ReturnsReservation()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);

            await _service.HoldAsync(productId, "sr-user-getall", 3, CancellationToken);

            var result = await _service.GetAllForUserAsync(new PresaleUserId("sr-user-getall"), CancellationToken);
            result.Count.ShouldBe(1);
            result[0].ProductId.Value.ShouldBe(productId);
            result[0].Quantity.Value.ShouldBe(3);
            result[0].Status.ShouldBe(SoftReservationStatus.Active);
        }

        // ── GetPriceChangesAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetPriceChangesAsync_WhenPriceNotChanged_ReturnsEmpty()
        {
            var productId = await SeedProductAsync(price: 55.00m, CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);
            await _service.HoldAsync(productId, "sr-user-priceok", 1, CancellationToken);

            var changes = await _service.GetPriceChangesAsync(new PresaleUserId("sr-user-priceok"), CancellationToken);

            // Catalog still returns the same price that was locked at hold time
            changes.ShouldBeEmpty();
        }

        // ── CommitAllForUserAsync ────────────────────────────────────────

        [Fact]
        public async Task CommitAllForUserAsync_SetsReservationStatusToCommitted()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);
            await _service.HoldAsync(productId, "sr-user-commit", 2, CancellationToken);

            await _service.CommitAllForUserAsync(new PresaleUserId("sr-user-commit"), CancellationToken);

            var reservations = await _service.GetAllForUserAsync(new PresaleUserId("sr-user-commit"), CancellationToken);
            reservations.ShouldNotBeEmpty();
            reservations[0].Status.ShouldBe(SoftReservationStatus.Committed);
        }

        // ── RevertAllForUserAsync ────────────────────────────────────────

        [Fact]
        public async Task RevertAllForUserAsync_AfterCommit_RevertsToActive()
        {
            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);
            await _service.HoldAsync(productId, "sr-user-revert", 1, CancellationToken);
            await _service.CommitAllForUserAsync(new PresaleUserId("sr-user-revert"), CancellationToken);

            await _service.RevertAllForUserAsync(new PresaleUserId("sr-user-revert"), CancellationToken);

            var reservations = await _service.GetAllForUserAsync(new PresaleUserId("sr-user-revert"), CancellationToken);
            reservations.ShouldNotBeEmpty();
            reservations[0].Status.ShouldBe(SoftReservationStatus.Active);
        }
    }
}

