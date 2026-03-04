using ECommerceApp.Application.Inventory.Availability.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    internal sealed class CheckoutSoftHoldService : ICheckoutSoftHoldService
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _cache;

        public CheckoutSoftHoldService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default)
        {
            var key = CacheKey(productId, userId);
            var hold = new SoftHold(productId, userId, quantity, DateTime.UtcNow.Add(Ttl));
            _cache.Set(key, hold, Ttl);
            return Task.CompletedTask;
        }

        public Task<SoftHold?> GetAsync(int productId, string userId, CancellationToken ct = default)
        {
            _cache.TryGetValue<SoftHold>(CacheKey(productId, userId), out var hold);
            return Task.FromResult(hold);
        }

        public Task RemoveAsync(int productId, string userId, CancellationToken ct = default)
        {
            _cache.Remove(CacheKey(productId, userId));
            return Task.CompletedTask;
        }

        private static string CacheKey(int productId, string userId) => $"softhold:{productId}:{userId}";
    }
}
