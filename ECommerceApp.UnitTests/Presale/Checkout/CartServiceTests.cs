using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using FluentAssertions;
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
        private readonly CartService _service;

        public CartServiceTests()
        {
            _cartRepo = new Mock<ICartLineRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _catalog = new Mock<ICatalogClient>();
            _catalog
                .Setup(c => c.GetProductsByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CatalogProductSummary>());
            _service = new CartService(_cartRepo.Object, _cache, _catalog.Object);
        }

        public void Dispose() => _cache.Dispose();

        // ── AddOrUpdateAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task AddOrUpdateAsync_ValidDto_ShouldUpsertAndRefreshCache()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 2) };
            _cartRepo.Setup(r => r.UpsertAsync(It.IsAny<CartLine>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _cartRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(lines);

            await _service.AddOrUpdateAsync(new AddToCartDto("user-1", 1, 2));

            _cartRepo.Verify(r => r.UpsertAsync(
                It.Is<CartLine>(l => l.UserId.Value == "user-1" && l.ProductId.Value == 1 && l.Quantity.Value == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddOrUpdateAsync_ValidDto_ShouldPopulateCache()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 2) };
            _cartRepo.Setup(r => r.UpsertAsync(It.IsAny<CartLine>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _cartRepo.Setup(r => r.GetByUserIdAsync(It.Is<PresaleUserId>(p => p.Value == "user-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lines);

            await _service.AddOrUpdateAsync(new AddToCartDto("user-1", 1, 2));

            var cart = await _service.GetCartAsync("user-1");
            cart.Should().NotBeNull();
            _cartRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── RemoveAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ExistingLine_ShouldDeleteAndRefreshCache()
        {
            _cartRepo.Setup(r => r.DeleteAsync("user-1", 1, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _cartRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CartLine>());

            await _service.RemoveAsync("user-1", 1);

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

            await _service.RemoveRangeAsync("user-1", productIds);

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

            await _service.ClearAsync("user-1");

            _cartRepo.Verify(r => r.DeleteAllForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
            _cache.TryGetValue("cart:user-1", out _).Should().BeFalse();
        }

        // ── GetCartAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetCartAsync_CacheMiss_ShouldLoadFromDbAndReturnVm()
        {
            var lines = new List<CartLine> { CartLine.Create("user-1", 1, 3) };
            _cartRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(lines);

            var result = await _service.GetCartAsync("user-1");

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

            var result = await _service.GetCartAsync("user-1");

            result.Should().BeSameAs(vm);
            _cartRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldReturnNull()
        {
            _cartRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CartLine>());

            var result = await _service.GetCartAsync("user-1");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldEvictStaleCache()
        {
            _cartRepo.Setup(r => r.GetByUserIdAsync(It.Is<PresaleUserId>(p => p.Value == "user-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CartLine>());

            await _service.GetCartAsync("user-1"); // cache miss → DB → empty

            _cache.TryGetValue("cart:user-1", out _).Should().BeFalse();
        }
    }
}
