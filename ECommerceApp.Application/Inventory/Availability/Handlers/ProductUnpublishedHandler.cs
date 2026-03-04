using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Inventory.Availability;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ProductUnpublishedHandler : IMessageHandler<ProductUnpublished>
    {
        private readonly IProductSnapshotRepository _snapshotRepo;

        public ProductUnpublishedHandler(IProductSnapshotRepository snapshotRepo)
        {
            _snapshotRepo = snapshotRepo;
        }

        public async Task HandleAsync(ProductUnpublished message, CancellationToken ct = default)
        {
            var existing = await _snapshotRepo.GetByProductIdAsync(message.ProductId, ct);
            if (existing is null)
                return;

            var updated = ProductSnapshot.Create(
                existing.ProductId,
                existing.ProductName,
                existing.IsDigital,
                CatalogProductStatus.Suspended);

            await _snapshotRepo.UpsertAsync(updated, ct);
        }
    }
}
