using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Orders;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class SnapshotOrderItemsJob : IScheduledTask
    {
        public const string JobTaskName = "SnapshotOrderItemsJob";
        private const int BatchSize = 64;

        public string TaskName => JobTaskName;

        private readonly IOrderItemRepository _orderItemRepo;
        private readonly IOrderProductResolver _productResolver;

        public SnapshotOrderItemsJob(
            IOrderItemRepository orderItemRepo,
            IOrderProductResolver productResolver)
        {
            _orderItemRepo = orderItemRepo;
            _productResolver = productResolver;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            var items = await _orderItemRepo.GetUnsnapshottedOrderItemsAsync(BatchSize, cancellationToken);

            if (items.Count == 0)
            {
                context.ReportSuccess("No unsnapshotted order items found.");
                return;
            }

            var productIds = items.Select(i => i.ItemId.Value).ToList();
            var resolved = await _productResolver.ResolveAllAsync(productIds, cancellationToken);

            var snapshots = items
                .Where(i => resolved.ContainsKey(i.ItemId.Value))
                .Select(i => (ItemId: i.Id?.Value ?? 0, Snapshot: resolved[i.ItemId.Value]))
                .ToList();

            var failedCount = items.Count - snapshots.Count;

            if (snapshots.Count > 0)
            {
                await _orderItemRepo.SetSnapshotsAsync(snapshots, cancellationToken);
            }

            context.ReportSuccess($"Snapshotted {snapshots.Count} item(s); {failedCount} product(s) not found.");
        }
    }
}
