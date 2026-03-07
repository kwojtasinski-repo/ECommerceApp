using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class SoftReservationService : ISoftReservationService
    {
        private readonly IMemoryCache _cache;
        private readonly IOptionsMonitor<PresaleOptions> _options;
        private readonly ConcurrentDictionary<int, HashSet<string>> _productUserIndex = new();

        public SoftReservationService(IMemoryCache cache, IOptionsMonitor<PresaleOptions> options)
        {
            _cache = cache;
            _options = options;
        }

        public Task HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default)
        {
            var ttl = _options.CurrentValue.SoftReservationTtl;
            var key = CacheKey(productId, userId);
            var reservation = new SoftReservation(productId, userId, quantity, DateTime.UtcNow.Add(ttl));
            _cache.Set(key, reservation, ttl);
            _productUserIndex.AddOrUpdate(
                productId,
                _ => new HashSet<string> { userId },
                (_, set) => { lock (set) { set.Add(userId); } return set; });
            return Task.CompletedTask;
        }

        public Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default)
        {
            _cache.TryGetValue<SoftReservation>(CacheKey(productId, userId), out var reservation);
            return Task.FromResult(reservation);
        }

        public Task RemoveAsync(int productId, string userId, CancellationToken ct = default)
        {
            _cache.Remove(CacheKey(productId, userId));
            if (_productUserIndex.TryGetValue(productId, out var set))
                lock (set) { set.Remove(userId); }
            return Task.CompletedTask;
        }

        public Task RemoveAllForProductAsync(int productId, CancellationToken ct = default)
        {
            if (!_productUserIndex.TryRemove(productId, out var set))
                return Task.CompletedTask;

            lock (set)
            {
                foreach (var userId in set)
                    _cache.Remove(CacheKey(productId, userId));
            }

            return Task.CompletedTask;
        }

        private static string CacheKey(int productId, string userId)
            => $"softreservation:{productId}:{userId}";
    }
}
