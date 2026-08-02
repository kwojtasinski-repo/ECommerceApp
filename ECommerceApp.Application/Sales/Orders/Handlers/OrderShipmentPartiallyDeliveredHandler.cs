using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.Events.Payloads;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentPartiallyDeliveredHandler : IIdAwareMessageHandler<ShipmentPartiallyDelivered>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IOrdersUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderShipmentPartiallyDeliveredHandler(
            IOrderRepository orderRepo,
            IOrdersUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _orderRepo = orderRepo;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(ShipmentPartiallyDelivered message, long outboxMessageId, CancellationToken ct = default)
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

                var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
                if (order is null)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                var deliveredItems = message.DeliveredItems
                    .Select(i => new FulfilledItem(i.ProductId, i.Quantity))
                    .ToList();
                var failedItems = message.FailedItems
                    .Select(i => new FulfilledItem(i.ProductId, i.Quantity))
                    .ToList();
                order.MarkAsPartiallyFulfilled(message.ShipmentId, deliveredItems, failedItems);
                await _orderRepo.UpdateAsync(order, ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
