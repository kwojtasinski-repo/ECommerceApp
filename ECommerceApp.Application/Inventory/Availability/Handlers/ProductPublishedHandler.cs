using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Inventory.Availability;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ProductPublishedHandler : IMessageHandler<ProductPublished>
    {
        private readonly IProductSnapshotRepository _snapshotRepo;

        public ProductPublishedHandler(IProductSnapshotRepository snapshotRepo)
        {
            _snapshotRepo = snapshotRepo;
        }

        public async Task HandleAsync(ProductPublished message, CancellationToken ct = default)
        {
            var snapshot = ProductSnapshot.Create(
                message.ProductId,
                message.ProductName,
                message.IsDigital,
                CatalogProductStatus.Orderable);

            await _snapshotRepo.UpsertAsync(snapshot, ct);
        }
    }
}
