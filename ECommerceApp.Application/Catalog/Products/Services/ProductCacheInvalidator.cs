using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class ProductCacheInvalidator : IProductCacheInvalidator
    {
        // Keep in sync with the key pattern in ProductService.GetProductDetails
        internal const string CatalogProductKeyPrefix = "CatalogProduct:";

        private readonly IMemoryCache _cache;

        public ProductCacheInvalidator(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void InvalidateDetails(int productId)
        {
            _cache.Remove($"{CatalogProductKeyPrefix}{productId}");
        }
    }
}
