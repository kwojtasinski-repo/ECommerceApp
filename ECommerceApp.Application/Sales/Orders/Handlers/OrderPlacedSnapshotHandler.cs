using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Collections.Generic;
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

            var snapshots = new List<(int ItemId, OrderProductSnapshot Snapshot)>();
            foreach (var item in items)
            {
                var snapshot = await _productResolver.ResolveAsync(item.ItemId.Value, ct);
                if (snapshot is not null)
                    snapshots.Add((item.Id.Value, snapshot));
            }

            if (snapshots.Count > 0)
                await _orderItemRepo.SetSnapshotsAsync(snapshots, ct);
        }
    }
}
