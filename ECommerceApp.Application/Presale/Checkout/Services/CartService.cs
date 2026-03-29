using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class CartService : ICartService
    {
        private readonly ICartLineRepository _cartRepo;
        private readonly IMemoryCache _cache;
        private readonly ICatalogClient _catalog;
        private readonly ICartRequirements _requirements;

        public CartService(ICartLineRepository cartRepo, IMemoryCache cache, ICatalogClient catalog, ICartRequirements requirements)
        {
            _cartRepo = cartRepo;
            _cache = cache;
            _catalog = catalog;
            _requirements = requirements;
        }

        public async Task<AddToCartResult> AddToCartAsync(AddToCartDto dto, CancellationToken ct = default)
        {
            var cart = await GetCartAsync(new PresaleUserId(dto.UserId), ct);
            var existingQty = cart?.Lines.FirstOrDefault(l => l.ProductId == dto.ProductId)?.Quantity ?? 0;
            if (existingQty + dto.Quantity > _requirements.MaxQuantityPerOrderLine)
                return AddToCartResult.LimitExceeded(_requirements.MaxQuantityPerOrderLine);

            var line = CartLine.Create(dto.UserId, dto.ProductId, existingQty + dto.Quantity);
            await _cartRepo.UpsertAsync(line, ct);
            await RefreshCacheAsync(new PresaleUserId(dto.UserId), ct);
            return AddToCartResult.Added();
        }

        public async Task SetCartItemAsync(AddToCartDto dto, CancellationToken ct = default)
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

        public async Task RemoveRangeAsync(PresaleUserId userId, IReadOnlyList<PresaleProductId> productIds, CancellationToken ct = default)
        {
            await _cartRepo.DeleteRangeAsync(userId, productIds, ct);
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

            var productIds = lines.Select(l => l.ProductId.Value).Distinct().ToList();
            var summaries = await _catalog.GetProductsByIdsAsync(productIds, ct);
            var nameMap = summaries.ToDictionary(s => s.Id, s => s.Name);

            var vm = new CartVm(userId, lines.Select(l => new CartLineVm(
                l.ProductId.Value,
                l.Quantity.Value,
                nameMap.TryGetValue(l.ProductId.Value, out var name) ? name : null)).ToList());
            _cache.Set(CacheKey(userId.Value), vm, TimeSpan.FromMinutes(30));
            return vm;
        }

        private static string CacheKey(string userId) => $"cart:{userId}";
    }
}
