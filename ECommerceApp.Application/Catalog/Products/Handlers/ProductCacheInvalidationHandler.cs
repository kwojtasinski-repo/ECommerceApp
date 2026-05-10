using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Handlers
{
    /// <summary>
    /// Evicts catalog-layer caches when a product's data changes.
    /// Registered four times — once per message type — all sharing the same key pattern.
    /// </summary>
    internal sealed class ProductCacheInvalidationHandler :
        IMessageHandler<ProductUpdated>,
        IMessageHandler<ProductPublished>,
        IMessageHandler<ProductUnpublished>,
        IMessageHandler<ProductDiscontinued>
    {
        // Keep in sync with the key pattern in ProductService.GetProductDetails
        internal const string CatalogProductKeyPrefix = "CatalogProduct:";

        private readonly IMemoryCache _cache;

        public ProductCacheInvalidationHandler(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task HandleAsync(ProductUpdated message, CancellationToken ct = default)
        {
            Evict(message.ProductId);
            return Task.CompletedTask;
        }

        public Task HandleAsync(ProductPublished message, CancellationToken ct = default)
        {
            Evict(message.ProductId);
            return Task.CompletedTask;
        }

        public Task HandleAsync(ProductUnpublished message, CancellationToken ct = default)
        {
            Evict(message.ProductId);
            return Task.CompletedTask;
        }

        public Task HandleAsync(ProductDiscontinued message, CancellationToken ct = default)
        {
            Evict(message.ProductId);
            return Task.CompletedTask;
        }

        private void Evict(int productId)
            => _cache.Remove($"{CatalogProductKeyPrefix}{productId}");
    }
}
