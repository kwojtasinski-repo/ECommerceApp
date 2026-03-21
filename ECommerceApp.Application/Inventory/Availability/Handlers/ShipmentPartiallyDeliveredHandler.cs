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
    internal sealed class ShipmentPartiallyDeliveredHandler : IMessageHandler<ShipmentPartiallyDelivered>
    {
        private readonly IStockService _stockService;
        private readonly IMessageBroker _messageBroker;

        public ShipmentPartiallyDeliveredHandler(IStockService stockService, IMessageBroker messageBroker)
        {
            _stockService = stockService;
            _messageBroker = messageBroker;
        }

        public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            var failures = new List<StockOperationFailure>();

            foreach (var item in message.DeliveredItems)
            {
                if (!await _stockService.FulfillAsync(message.OrderId, item.ProductId, item.Quantity, ct))
                    failures.Add(new StockOperationFailure(item.ProductId, item.Quantity, StockOperationType.Fulfill));
            }

            foreach (var item in message.FailedItems)
            {
                if (!await _stockService.ReleaseAsync(message.OrderId, item.ProductId, item.Quantity, ct))
                    failures.Add(new StockOperationFailure(item.ProductId, item.Quantity, StockOperationType.Release));
            }

            if (failures.Count > 0)
                await _messageBroker.PublishAsync(new StockReconciliationRequired(message.OrderId, failures, DateTime.UtcNow));
        }
    }
}
