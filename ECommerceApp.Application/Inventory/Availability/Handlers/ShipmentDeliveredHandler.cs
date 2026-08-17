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
    internal sealed class ShipmentDeliveredHandler : ShipmentStockOutcomeHandlerBase, IIdAwareMessageHandler<ShipmentDelivered>
    {
        public ShipmentDeliveredHandler(
            IStockService stockService,
            IInventoryUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IProcessedMessageGuard processedMessageGuard)
            : base(stockService, unitOfWork, outboxWriter, processedMessageGuard)
        {
        }

        public Task HandleAsync(ShipmentDelivered message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public Task HandleAsync(ShipmentDelivered message, long outboxMessageId, CancellationToken ct = default)
        {
            var operations = message.Items
                .Select(item => new StockOperation(item.ProductId, item.Quantity, StockOperationType.Fulfill))
                .ToList();

            return ProcessAsync(message.OrderId, operations, outboxMessageId, ct);
        }
    }
}
