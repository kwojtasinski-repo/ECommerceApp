using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using Microsoft.AspNetCore.OutputCaching;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Handlers
{
    /// <summary>
    /// Evicts the full-response OutputCache for the storefront index page when any product
    /// metadata changes (published, unpublished, updated, discontinued).
    /// Lives in Web because IOutputCacheStore is a web-infrastructure concern.
    /// </summary>
    internal sealed class StorefrontOutputCacheHandler :
        IMessageHandler<ProductUpdated>,
        IMessageHandler<ProductPublished>,
        IMessageHandler<ProductUnpublished>,
        IMessageHandler<ProductDiscontinued>
    {
        /// <summary>Tag applied to every cached storefront-index response.</summary>
        internal const string StorefrontIndexTag = "storefront-index";

        private readonly IOutputCacheStore _store;

        public StorefrontOutputCacheHandler(IOutputCacheStore store)
        {
            _store = store;
        }

        public Task HandleAsync(ProductUpdated message, CancellationToken ct = default)
            => EvictAsync(ct);

        public Task HandleAsync(ProductPublished message, CancellationToken ct = default)
            => EvictAsync(ct);

        public Task HandleAsync(ProductUnpublished message, CancellationToken ct = default)
            => EvictAsync(ct);

        public Task HandleAsync(ProductDiscontinued message, CancellationToken ct = default)
            => EvictAsync(ct);

        private Task EvictAsync(CancellationToken ct)
            => _store.EvictByTagAsync(StorefrontIndexTag, ct).AsTask();
    }
}
