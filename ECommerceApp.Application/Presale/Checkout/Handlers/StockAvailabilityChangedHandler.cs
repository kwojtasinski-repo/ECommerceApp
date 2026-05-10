using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class StockAvailabilityChangedHandler : IMessageHandler<StockAvailabilityChanged>
    {
        private readonly IStockSnapshotRepository _snapshotRepo;
        private readonly IMemoryCache _cache;

        public StockAvailabilityChangedHandler(IStockSnapshotRepository snapshotRepo, IMemoryCache cache)
        {
            _snapshotRepo = snapshotRepo;
            _cache = cache;
        }

        public async Task HandleAsync(StockAvailabilityChanged message, CancellationToken ct = default)
        {
            var snapshot = await _snapshotRepo.FindByProductIdAsync(message.ProductId, ct);
            if (snapshot is null)
            {
                snapshot = StockSnapshot.Create(message.ProductId, message.AvailableQuantity, message.OccurredAt);
                await _snapshotRepo.AddAsync(snapshot, ct);
            }
            else
            {
                snapshot.Update(message.AvailableQuantity, message.OccurredAt);
                await _snapshotRepo.UpdateAsync(snapshot, ct);
            }

            // Evict the storefront product-details cache so the next request reflects
            // the updated stock level (stock is included in StorefrontProductDetailsVm).
            _cache.Remove($"{ProductDetailsCacheInvalidationHandler.ProductDetailsCacheKeyPrefix}{message.ProductId}");
        }
    }
}
