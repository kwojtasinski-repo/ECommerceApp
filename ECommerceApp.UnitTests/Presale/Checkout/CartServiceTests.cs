using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class CartServiceTests : IDisposable
    {
        private readonly Mock<ICartLineRepository> _cartRepo;
        private readonly IMemoryCache _cache;
        private readonly Mock<ICatalogClient> _catalog;
        private readonly Mock<ICartRequirements> _requirements;
        private readonly CartService _service;

        public CartServiceTests()
        {
            _cartRepo = new Mock<ICartLineRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _catalog = new Mock<ICatalogClient>();
            _requirements = new Mock<ICartRequirements>();
            _catalog
                .Setup(c => c.GetProductsByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CatalogProductSummary>());
            _requirements.Setup(r => r.MaxQuantityPerOrderLine).Returns(10);
            _service = new CartService(_cartRepo.Object, _cache, _catalog.Object, _requirements.Object);
        }

        public void Dispose() => _cache.Dispose();

        private void SetupCartLines(string userId, IReadOnlyList<CartLine> lines)
        {
            _cartRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(lines);
        }

        private void SetupCartLines(IReadOnlyList<CartLine> lines)
        {
            _cartRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lines);
        }

        private void SetupCartUpsert()
        {
            _cartRepo.Setup(r => r.UpsertAsync(It.IsAny<CartLine>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupCartDelete(string userId, int productId)
        {
            _cartRepo.Setup(r => r.DeleteAsync(userId, productId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // ── SetCartItemAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task SetCartItemAsync_ValidDto_ShouldUpsertAndRefreshCache()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 2) };
            SetupCartUpsert();
            SetupCartLines("user-1", lines);

            await _service.SetCartItemAsync(new AddToCartDto("user-1", 1, 2), TestContext.Current.CancellationToken);

            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.UserId.Value == "user-1" && l.ProductId.Value == 1 && l.Quantity.Value == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetCartItemAsync_ValidDto_ShouldPopulateCache()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 2) };
            SetupCartUpsert();
            _cartRepo.Setup(r => r.GetByUserIdAsync(It.Is<PresaleUserId>(p => p.Value == "user-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lines);

            await _service.SetCartItemAsync(new AddToCartDto("user-1", 1, 2), TestContext.Current.CancellationToken);

            var cart = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);
            cart.Should().NotBeNull();
            _cartRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── RestoreAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task RestoreAsync_MultipleItems_ShouldUpsertEachLineAndRefreshCache()
        {
            var lines = new List<CartLine>
            {
                CartLine.Create("user-1", 1, 2),
                CartLine.Create("user-1", 2, 3)
            };

            SetupCartUpsert();
            SetupCartLines(lines);

            await _service.RestoreAsync(
                new PresaleUserId("user-1"),
                new List<CartRestoreItem> { new(1, 2), new(2, 3) },
                TestContext.Current.CancellationToken);

            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.UserId.Value == "user-1" && l.ProductId.Value == 1 && l.Quantity.Value == 2),
                It.IsAny<CancellationToken>()), Times.Once);
            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.UserId.Value == "user-1" && l.ProductId.Value == 2 && l.Quantity.Value == 3),
                It.IsAny<CancellationToken>()), Times.Once);

            var cart = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);
            cart.Should().NotBeNull();
            cart!.Lines.Count.Should().Be(2);
            _cartRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestoreAsync_ProductNotInCatalog_ShouldStillRestoreLine()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 7, 4) };

            SetupCartUpsert();
            SetupCartLines(lines);

            await _service.RestoreAsync(
                new PresaleUserId("user-1"),
                new List<CartRestoreItem> { new(7, 4) },
                TestContext.Current.CancellationToken);

            var cart = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);
            cart.Should().NotBeNull();
            cart!.Lines.Count.Should().Be(1);
            cart.Lines[0].ProductId.Should().Be(7);
            cart.Lines[0].Quantity.Should().Be(4);
            _cartRepo.Verify(r => r.UpsertAsync(It.IsAny<CartLine>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── AddToCartAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task AddToCartAsync_NoExistingItem_ShouldSetQuantityToRequested()
        {
            SetupCartUpsert();
            SetupCartLines(new List<CartLine>());

            var result = await _service.AddToCartAsync(new AddToCartDto("user-1", 1, 3), TestContext.Current.CancellationToken);

            result.Should().BeOfType<AddToCartResult.Success>();
            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.ProductId.Value == 1 && l.Quantity.Value == 3),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddToCartAsync_ExistingItem_ShouldIncrementQuantity()
        {
            var existing = new List<CartLine> { CartLine.Create("user-1", 1, 4) };
            SetupCartUpsert();
            SetupCartLines(existing);

            var result = await _service.AddToCartAsync(new AddToCartDto("user-1", 1, 3), TestContext.Current.CancellationToken);

            result.Should().BeOfType<AddToCartResult.Success>();
            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.ProductId.Value == 1 && l.Quantity.Value == 7), // 4 + 3
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddToCartAsync_ExceedsLimit_ShouldReturnQuantityExceeded()
        {
            var existing = new List<CartLine> { CartLine.Create("user-1", 1, 8) };
            SetupCartLines(existing);

            var result = await _service.AddToCartAsync(new AddToCartDto("user-1", 1, 5), TestContext.Current.CancellationToken);

            result.Should().BeOfType<AddToCartResult.QuantityExceeded>()
                .Which.MaxAllowed.Should().Be(10);
            _cartRepo.Verify(r => r.UpsertAsync(It.IsAny<CartLine>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── RemoveAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ExistingLine_ShouldDeleteAndRefreshCache()
        {
            SetupCartDelete("user-1", 1);
            SetupCartLines("user-1", new List<CartLine>());

            await _service.RemoveAsync("user-1", 1, TestContext.Current.CancellationToken);

            _cartRepo.Verify(r => r.DeleteAsync("user-1", 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── RemoveRangeAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task RemoveRangeAsync_MultipleProducts_ShouldDeleteAllInOneCallAndRefreshCache()
        {
            var productIds = new List<PresaleProductId> { new(10), new(20) };
            _cartRepo.Setup(r => r.DeleteRangeAsync(
                    It.IsAny<PresaleUserId>(), It.IsAny<IReadOnlyList<PresaleProductId>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _cartRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CartLine>());

            await _service.RemoveRangeAsync("user-1", productIds, TestContext.Current.CancellationToken);

            _cartRepo.Verify(r => r.DeleteRangeAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-1"),
                It.Is<IReadOnlyList<PresaleProductId>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
            _cartRepo.Verify(r => r.DeleteAsync(
                It.IsAny<PresaleUserId>(), It.IsAny<PresaleProductId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ClearAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task ClearAsync_WithLines_ShouldDeleteAllAndEvictCache()
        {
            _cartRepo.Setup(r => r.DeleteAllForUserAsync("user-1", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _cache.Set("cart:user-1", new CartVm("user-1", new List<CartLineVm>()));

            await _service.ClearAsync("user-1", TestContext.Current.CancellationToken);

            _cartRepo.Verify(r => r.DeleteAllForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
            _cache.TryGetValue("cart:user-1", out _).Should().BeFalse();
        }

        // ── GetCartAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetCartAsync_CacheMiss_ShouldLoadFromDbAndReturnVm()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 3) };
            SetupCartLines("user-1", lines);

            var result = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result!.UserId.Should().Be("user-1");
            result.Lines.Should().HaveCount(1);
            result.Lines[0].ProductId.Should().Be(1);
            result.Lines[0].Quantity.Should().Be(3);
        }

        [Fact]
        public async Task GetCartAsync_CacheHit_ShouldReturnCachedValueWithoutDbCall()
        {
            var vm = new CartVm("user-1", new List<CartLineVm> { new(1, 3, null) });
            _cache.Set("cart:user-1", vm, TimeSpan.FromMinutes(30));

            var result = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);

            result.Should().BeSameAs(vm);
            _cartRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldReturnNull()
        {
            SetupCartLines("user-1", new List<CartLine>());

            var result = await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldEvictStaleCache()
        {
            SetupCartLines(new List<CartLine>());

            await _service.GetCartAsync("user-1", TestContext.Current.CancellationToken); // cache miss → DB → empty

            _cache.TryGetValue("cart:user-1", out _).Should().BeFalse();
        }
    }
}
