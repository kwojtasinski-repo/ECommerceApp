using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderPlacedSnapshotHandler : IMessageHandler<OrderPlaced>
    {
        private readonly IOrderItemRepository _orderItemRepo;
        private readonly IOrderProductResolver _productResolver;

        public OrderPlacedSnapshotHandler(
            IOrderItemRepository orderItemRepo,
            IOrderProductResolver productResolver)
        {
            _orderItemRepo = orderItemRepo;
            _productResolver = productResolver;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            var items = await _orderItemRepo.GetByOrderIdAsync(message.OrderId, ct);
            if (items.Count == 0)
                return;

            var productIds = items.Select(i => i.ItemId.Value).ToList();
            var resolved = await _productResolver.ResolveAllAsync(productIds, ct);

            var snapshots = items
                .Where(i => resolved.ContainsKey(i.ItemId.Value))
                .Select(i => (ItemId: i.Id?.Value ?? 0, Snapshot: resolved[i.ItemId.Value]))
                .ToList();

            if (snapshots.Count > 0)
            {
                await _orderItemRepo.SetSnapshotsAsync(snapshots, ct);
            }
        }
    }
}
