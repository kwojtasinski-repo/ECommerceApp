using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal abstract class ShipmentStockOutcomeHandlerBase
    {
        protected readonly record struct StockOperation(int ProductId, int Quantity, StockOperationType Type);

        private readonly IStockService _stockService;
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        protected ShipmentStockOutcomeHandlerBase(
            IStockService stockService,
            IInventoryUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IProcessedMessageGuard processedMessageGuard)
        {
            _stockService = stockService;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _processedMessageGuard = processedMessageGuard;
        }

        protected async Task ProcessAsync(
            int orderId,
            IReadOnlyList<StockOperation> operations,
            long outboxMessageId,
            CancellationToken ct)
        {
            var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            await using (transaction)
            {
                var handlerType = GetType().FullName
                    ?? throw new InvalidOperationException("Handler type name is unavailable.");
                if (!await _processedMessageGuard.TryMarkProcessedAsync(outboxMessageId, handlerType, transaction, ct))
                {
                    return;
                }

                var failures = new List<StockOperationFailure>();

                foreach (var op in operations)
                {
                    var succeeded = op.Type == StockOperationType.Fulfill
                        ? await _stockService.FulfillAsync(orderId, op.ProductId, op.Quantity, ct)
                        : await _stockService.ReleaseAsync(orderId, op.ProductId, op.Quantity, ct);

                    if (!succeeded)
                        failures.Add(new StockOperationFailure(op.ProductId, op.Quantity, op.Type));
                }

                if (failures.Count > 0)
                {
                    await _outboxWriter.EnqueueAsync(
                        new StockReconciliationRequired(orderId, failures, DateTime.UtcNow),
                        transaction,
                        ct);
                }

                await transaction.CommitAsync(ct);
            }
        }
    }
}
