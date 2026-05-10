using ECommerceApp.Application.Constants;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class StorefrontQueryService : IStorefrontQueryService
    {
        private readonly ICatalogClient _catalog;
        private readonly IStockSnapshotRepository _stockSnapshots;
        private readonly IMemoryCache _cache;
        private readonly CacheOptions _cacheOptions;

        public StorefrontQueryService(ICatalogClient catalog, IStockSnapshotRepository stockSnapshots, IMemoryCache cache, IOptions<CacheOptions> cacheOptions)
        {
            _catalog = catalog;
            _stockSnapshots = stockSnapshots;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        public async Task<StorefrontProductListVm> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, int? categoryId = null, CancellationToken ct = default)
        {
            var productList = await _catalog.GetPublishedProductsAsync(pageSize, pageNo, searchString, ct, categoryId);

            var productIds = productList.Products.Select(p => p.Id).ToList();
            var snapshotByProductId = new Dictionary<int, StockSnapshot>(productIds.Count);
            await foreach (var s in _stockSnapshots.GetByProductIdsAsync(productIds, ct))
            {
                snapshotByProductId[s.ProductId.Value] = s;
            }

            var items = productList.Products.Select(p =>
            {
                snapshotByProductId.TryGetValue(p.Id, out var snapshot);
                var available = snapshot?.AvailableQuantity ?? 0;
                return new StorefrontProductVm(p.Id, p.Name, p.Cost, p.CategoryId, available, available > 0, p.MainImageUrl);
            }).ToList();

            return new StorefrontProductListVm(
                items,
                productList.Count,
                productList.PageSize,
                productList.CurrentPage,
                productList.SearchString ?? "");
        }

        public async Task<StorefrontProductListVm> GetPublishedProductsByTagAsync(
            int tagId, int pageSize, int pageNo, CancellationToken ct = default)
        {
            var productList = await _catalog.GetPublishedProductsByTagAsync(tagId, pageSize, pageNo, ct);

            var productIds = productList.Products.Select(p => p.Id).ToList();
            var snapshotByProductId = new Dictionary<int, StockSnapshot>(productIds.Count);
            await foreach (var s in _stockSnapshots.GetByProductIdsAsync(productIds, ct))
            {
                snapshotByProductId[s.ProductId.Value] = s;
            }

            var items = productList.Products.Select(p =>
            {
                snapshotByProductId.TryGetValue(p.Id, out var snapshot);
                var available = snapshot?.AvailableQuantity ?? 0;
                return new StorefrontProductVm(p.Id, p.Name, p.Cost, p.CategoryId, available, available > 0, p.MainImageUrl);
            }).ToList();

            return new StorefrontProductListVm(items, productList.Count, productList.PageSize, productList.CurrentPage, string.Empty);
        }
        public async Task<StorefrontProductDetailsVm> GetProductDetailsAsync(int productId, CancellationToken ct = default)
        {
            var cacheKey = $"{ProductDetailsCacheInvalidationHandler.ProductDetailsCacheKeyPrefix}{productId}";
            if (_cache.TryGetValue(cacheKey, out StorefrontProductDetailsVm cachedVm))
                return cachedVm;

            var product = await _catalog.GetProductDetailsAsync(productId, ct);
            if (product is null)
                return null;

            StockSnapshot snapshot = null;
            await foreach (var s in _stockSnapshots.GetByProductIdsAsync(new List<int> { productId }, ct))
            {
                snapshot = s;
            }

            var available = snapshot?.AvailableQuantity ?? 0;
            var vm = new StorefrontProductDetailsVm(
                product.Id, product.Name, product.Cost, product.Description, product.CategoryName,
                product.Images, product.TagIds, product.TagNames, available, available > 0);

            _cache.Set(cacheKey, vm, _cacheOptions.ProductDetailsTtl);
            return vm;
        }

        public Task<IReadOnlyList<CatalogTagSummary>> GetAllTagsAsync(CancellationToken ct = default)
            => _catalog.GetAllTagsAsync(ct);

        public Task<CatalogTagSummary> GetTagByIdAsync(int tagId, CancellationToken ct = default)
            => _catalog.GetTagByIdAsync(tagId, ct);
    }
}
