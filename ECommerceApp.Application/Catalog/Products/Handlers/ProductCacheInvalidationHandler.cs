using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Catalog.Products.Services;
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
        private readonly IProductCacheInvalidator _invalidator;

        public ProductCacheInvalidationHandler(IProductCacheInvalidator invalidator)
        {
            _invalidator = invalidator;
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
            => _invalidator.InvalidateDetails(productId);
    }
}
