using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class CartService : ICartService
    {
        private readonly ICartLineRepository _cartRepo;
        private readonly IMemoryCache _cache;

        public CartService(ICartLineRepository cartRepo, IMemoryCache cache)
        {
            _cartRepo = cartRepo;
            _cache = cache;
        }

        public async Task AddOrUpdateAsync(AddToCartDto dto, CancellationToken ct = default)
        {
            var line = CartLine.Create(dto.UserId, dto.ProductId, dto.Quantity);
            await _cartRepo.UpsertAsync(line, ct);
            await RefreshCacheAsync(dto.UserId, ct);
        }

        public async Task RemoveAsync(PresaleUserId userId, PresaleProductId productId, CancellationToken ct = default)
        {
            await _cartRepo.DeleteAsync(userId, productId, ct);
            await RefreshCacheAsync(userId, ct);
        }

        public async Task ClearAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            await _cartRepo.DeleteAllForUserAsync(userId, ct);
            _cache.Remove(CacheKey(userId.Value));
        }

        public async Task<CartVm?> GetCartAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            if (_cache.TryGetValue<CartVm>(CacheKey(userId.Value), out var cached))
            {
                return cached;
            }

            return await RefreshCacheAsync(userId, ct);
        }

        private async Task<CartVm?> RefreshCacheAsync(PresaleUserId userId, CancellationToken ct)
        {
            var lines = await _cartRepo.GetByUserIdAsync(userId, ct);
            if (lines.Count == 0)
            {
                _cache.Remove(CacheKey(userId.Value));
                return null;
            }

            var vm = new CartVm(userId, lines.Select(l => new CartLineVm(l.ProductId.Value, l.Quantity.Value)).ToList());
            _cache.Set(CacheKey(userId.Value), vm, TimeSpan.FromMinutes(30));
            return vm;
        }

        private static string CacheKey(string userId) => $"cart:{userId}";
    }
}
