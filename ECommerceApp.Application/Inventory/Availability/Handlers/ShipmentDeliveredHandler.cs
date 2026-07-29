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
    internal sealed class ShipmentDeliveredHandler : IMessageHandler<ShipmentDelivered>
    {
        private readonly IStockService _stockService;
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;

        public ShipmentDeliveredHandler(IStockService stockService, IInventoryUnitOfWork unitOfWork, IOutboxWriter outboxWriter)
        {
            _stockService = stockService;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
        }

        public async Task HandleAsync(ShipmentDelivered message, CancellationToken ct = default)
        {
            var failures = new List<StockOperationFailure>();

            foreach (var item in message.Items)
            {
                if (!await _stockService.FulfillAsync(message.OrderId, item.ProductId, item.Quantity, ct))
                    failures.Add(new StockOperationFailure(item.ProductId, item.Quantity, StockOperationType.Fulfill));
            }

            if (failures.Count > 0)
            {
                var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
                await using (transaction)
                {
                    await _outboxWriter.EnqueueAsync(
                        new StockReconciliationRequired(message.OrderId, failures, DateTime.UtcNow),
                        transaction,
                        CancellationToken.None);
                    await transaction.CommitAsync(CancellationToken.None);
                }
            }
        }
    }
}
