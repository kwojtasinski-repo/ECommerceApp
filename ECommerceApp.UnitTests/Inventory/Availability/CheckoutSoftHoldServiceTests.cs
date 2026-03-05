using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class CheckoutSoftHoldServiceTests : IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly CheckoutSoftHoldService _service;

        public CheckoutSoftHoldServiceTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            var options = new Mock<IOptionsMonitor<InventoryOptions>>();
            options.Setup(o => o.CurrentValue).Returns(new InventoryOptions());
            _service = new CheckoutSoftHoldService(_cache, options.Object);
        }

        public void Dispose() => _cache.Dispose();

        [Fact]
        public async Task HoldAsync_ValidInput_ShouldStoreHoldInCache()
        {
            await _service.HoldAsync(1, "user-1", 3);

            var hold = await _service.GetAsync(1, "user-1");

            hold.Should().NotBeNull();
            hold!.ProductId.Should().Be(1);
            hold.UserId.Should().Be("user-1");
            hold.Quantity.Should().Be(3);
            hold.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task GetAsync_WhenHoldNotExists_ShouldReturnNull()
        {
            var hold = await _service.GetAsync(99, "user-x");

            hold.Should().BeNull();
        }

        [Fact]
        public async Task HoldAsync_CalledTwice_ShouldOverwriteWithLatestValue()
        {
            await _service.HoldAsync(1, "user-1", 3);
            await _service.HoldAsync(1, "user-1", 7);

            var hold = await _service.GetAsync(1, "user-1");

            hold!.Quantity.Should().Be(7);
        }

        [Fact]
        public async Task RemoveAsync_ExistingHold_ShouldDeleteFromCache()
        {
            await _service.HoldAsync(1, "user-1", 5);

            await _service.RemoveAsync(1, "user-1");

            var hold = await _service.GetAsync(1, "user-1");
            hold.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_NonExistingHold_ShouldNotThrow()
        {
            var act = async () => await _service.RemoveAsync(99, "nobody");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task HoldAsync_DifferentUsers_ShouldBeIsolated()
        {
            await _service.HoldAsync(1, "user-1", 2);
            await _service.HoldAsync(1, "user-2", 9);

            var hold1 = await _service.GetAsync(1, "user-1");
            var hold2 = await _service.GetAsync(1, "user-2");

            hold1!.Quantity.Should().Be(2);
            hold2!.Quantity.Should().Be(9);
        }

        [Fact]
        public async Task HoldAsync_DifferentProducts_ShouldBeIsolated()
        {
            await _service.HoldAsync(1, "user-1", 2);
            await _service.HoldAsync(2, "user-1", 5);

            var hold1 = await _service.GetAsync(1, "user-1");
            var hold2 = await _service.GetAsync(2, "user-1");

            hold1!.Quantity.Should().Be(2);
            hold2!.Quantity.Should().Be(5);
        }

        [Fact]
        public async Task GetAsync_SoftHold_ShouldReturnCorrectRecord()
        {
            await _service.HoldAsync(42, "user-abc", 10);

            var hold = await _service.GetAsync(42, "user-abc");

            hold.Should().BeOfType<SoftHold>();
            hold!.ProductId.Should().Be(42);
            hold.UserId.Should().Be("user-abc");
            hold.Quantity.Should().Be(10);
        }
    }
}
