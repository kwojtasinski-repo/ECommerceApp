using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    /// <summary>
    /// Evicts the storefront product-details cache when a product's metadata changes.
    /// Keeps in sync with the key pattern used by StorefrontQueryService.GetProductDetailsAsync.
    /// </summary>
    internal sealed class ProductDetailsCacheInvalidationHandler :
        IMessageHandler<ProductUpdated>,
        IMessageHandler<ProductPublished>,
        IMessageHandler<ProductUnpublished>,
        IMessageHandler<ProductDiscontinued>
    {
        // Keep in sync with StorefrontQueryService.ProductDetailsCacheKeyPrefix
        internal const string ProductDetailsCacheKeyPrefix = "ProductDetails:";

        private readonly IMemoryCache _cache;

        public ProductDetailsCacheInvalidationHandler(IMemoryCache cache)
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
            => _cache.Remove($"{ProductDetailsCacheKeyPrefix}{productId}");
    }
}
