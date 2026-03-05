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

        public StockAdjustmentJob(
            IStockItemRepository stockItemRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo)
        {
            _stockItemRepo = stockItemRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
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

            const int maxAttempts = 5;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(productId, cancellationToken);
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

            context.ReportSuccess($"Stock adjusted to {pending.NewQuantity} for product {productId}.");
        }
    }
}
