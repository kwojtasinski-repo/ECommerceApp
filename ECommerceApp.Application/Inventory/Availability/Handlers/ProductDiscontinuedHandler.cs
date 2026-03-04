using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Inventory.Availability;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ProductDiscontinuedHandler : IMessageHandler<ProductDiscontinued>
    {
        private readonly IProductSnapshotRepository _snapshotRepo;

        public ProductDiscontinuedHandler(IProductSnapshotRepository snapshotRepo)
        {
            _snapshotRepo = snapshotRepo;
        }

        public async Task HandleAsync(ProductDiscontinued message, CancellationToken ct = default)
        {
            var existing = await _snapshotRepo.GetByProductIdAsync(message.ProductId, ct);
            if (existing is null)
                return;

            var updated = ProductSnapshot.Create(
                existing.ProductId,
                existing.ProductName,
                existing.IsDigital,
                CatalogProductStatus.Discontinued);

            await _snapshotRepo.UpsertAsync(updated, ct);
        }
    }
}
