using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class StockAvailabilityChangedHandler : IMessageHandler<StockAvailabilityChanged>
    {
        private readonly IStockSnapshotRepository _snapshotRepo;

        public StockAvailabilityChangedHandler(IStockSnapshotRepository snapshotRepo)
        {
            _snapshotRepo = snapshotRepo;
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
        }
    }
}
