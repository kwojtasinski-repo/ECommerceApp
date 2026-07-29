using ECommerceApp.Application.Inventory.Availability;
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
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IStockAuditRepository _auditRepo;

        public StockAdjustmentJob(
            IStockItemRepository stockItemRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo,
            IInventoryUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IStockAuditRepository auditRepo)
        {
            _stockItemRepo = stockItemRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
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

            // Deliberately NOT wrapped in the same transaction as the retry loop above: opening a
            // CrossContextTransactionScope before the loop would hold a DB transaction open across the
            // exponential-backoff Task.Delay calls, which is a real risk (long-held locks/connection),
            // not just style. So the stock quantity update (already committed by the loop's own
            // UpdateAsync/SaveChangesAsync) and this audit+outbox write are two separate commits — an
            // acknowledged, narrow atomicity gap for this job specifically (residual-risk exception per
            // the Phase 3 retrofit plan), not an oversight.
            var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Adjusted, adjustBefore, stock!.AvailableQuantity, null, DateTime.UtcNow), cancellationToken);
                await _outboxWriter.EnqueueAsync(
                    new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow),
                    transaction,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            context.ReportSuccess($"Stock adjusted to {pending.NewQuantity} for product {productId}.");
        }
    }
}
