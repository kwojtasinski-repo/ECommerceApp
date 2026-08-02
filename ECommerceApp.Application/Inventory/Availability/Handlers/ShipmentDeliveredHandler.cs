using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ShipmentDeliveredHandler : IIdAwareMessageHandler<ShipmentDelivered>
    {
        private readonly IStockService _stockService;
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public ShipmentDeliveredHandler(
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

        public async Task HandleAsync(ShipmentDelivered message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(ShipmentDelivered message, long outboxMessageId, CancellationToken ct = default)
        {
            var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            await using (transaction)
            {
                var handlerType = GetType().FullName
                    ?? throw new InvalidOperationException("Handler type name is unavailable.");
                if (!await _processedMessageGuard.TryMarkProcessedAsync(
                        outboxMessageId, handlerType, transaction, ct))
                {
                    return;
                }

                var failures = new List<StockOperationFailure>();

                foreach (var item in message.Items)
                {
                    if (!await _stockService.FulfillAsync(message.OrderId, item.ProductId, item.Quantity, ct))
                        failures.Add(new StockOperationFailure(item.ProductId, item.Quantity, StockOperationType.Fulfill));
                }

                if (failures.Count > 0)
                {
                    await _outboxWriter.EnqueueAsync(
                        new StockReconciliationRequired(message.OrderId, failures, DateTime.UtcNow),
                        transaction,
                        ct);
                }

                await transaction.CommitAsync(ct);
            }
        }
    }
}
