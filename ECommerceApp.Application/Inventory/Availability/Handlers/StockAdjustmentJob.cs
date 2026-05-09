using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class StockAdjustmentJob : IScheduledTask
    {
        public const string JobTaskName = "StockAdjustmentJob";

        public string TaskName => JobTaskName;

        private readonly IStockItemRepository _stockItemRepo;
        private readonly IPendingStockAdjustmentRepository _pendingAdjustmentRepo;
        private readonly IMessageBroker _broker;
        private readonly IStockAuditRepository _auditRepo;

        public StockAdjustmentJob(
            IStockItemRepository stockItemRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo,
            IMessageBroker broker,
            IStockAuditRepository auditRepo)
        {
            _stockItemRepo = stockItemRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
            _broker = broker;
            _auditRepo = auditRepo;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (context.EntityId is null)
            {
                context.ReportFailure("Missing EntityId.");
                return;
            }

            if (!int.TryParse(context.EntityId, out var productId))
            {
                context.ReportFailure($"Invalid EntityId: {context.EntityId}");
                return;
            }

            var pending = await _pendingAdjustmentRepo.GetByProductIdAsync(productId, cancellationToken);
            if (pending is null)
            {
                context.ReportSuccess("No pending adjustment — already handled.");
                return;
            }

            var version = pending.Version;
            StockItem stock = null;
            int adjustBefore = 0;

            const int maxAttempts = 5;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                stock = await _stockItemRepo.GetByProductIdAsync(productId, cancellationToken);
                if (stock is null)
                {
                    context.ReportFailure($"Stock not found for product '{productId}'.");
                    return;
                }

                if (pending.NewQuantity.Value < stock.ReservedQuantity.Value)
                {
                    context.ReportFailure($"Cannot adjust stock to {pending.NewQuantity} — {stock.ReservedQuantity} units currently reserved for product '{productId}'.");
                    return;
                }

                adjustBefore = stock.AvailableQuantity;
                stock.Adjust(pending.NewQuantity);

                try
                {
                    await _stockItemRepo.UpdateAsync(stock, cancellationToken);
                    break;
                }
                catch (DbUpdateConcurrencyException) when (attempt < maxAttempts - 1)
                {
                    await Task.Delay((int)(100 * Math.Pow(2, attempt)), cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    context.ReportFailure("Adjustment failed after max retries.");
                    return;
                }
            }

            await _pendingAdjustmentRepo.DeleteIfVersionMatchesAsync(productId, version, cancellationToken);
            await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Adjusted, adjustBefore, stock!.AvailableQuantity, null, DateTime.UtcNow), cancellationToken);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));

            context.ReportSuccess($"Stock adjusted to {pending.NewQuantity} for product {productId}.");
        }
    }
}
