using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class ShipmentPartiallyDeliveredHandler : ShipmentStockOutcomeHandlerBase, IIdAwareMessageHandler<ShipmentPartiallyDelivered>
    {
        public ShipmentPartiallyDeliveredHandler(
            IStockService stockService,
            IInventoryUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IProcessedMessageGuard processedMessageGuard)
            : base(stockService, unitOfWork, outboxWriter, processedMessageGuard)
        {
        }

        public Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public Task HandleAsync(ShipmentPartiallyDelivered message, long outboxMessageId, CancellationToken ct = default)
        {
            var operations = message.DeliveredItems
                .Select(item => new StockOperation(item.ProductId, item.Quantity, StockOperationType.Fulfill))
                .Concat(message.FailedItems
                    .Select(item => new StockOperation(item.ProductId, item.Quantity, StockOperationType.Release)))
                .ToList();

            return ProcessAsync(message.OrderId, operations, outboxMessageId, ct);
        }
    }
}
