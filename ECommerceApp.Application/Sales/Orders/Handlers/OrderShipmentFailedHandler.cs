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
    internal sealed class OrderShipmentFailedHandler : IIdAwareMessageHandler<ShipmentFailed>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IOrdersUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderShipmentFailedHandler(
            IOrderRepository orderRepo,
            IOrdersUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _orderRepo = orderRepo;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(ShipmentFailed message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(ShipmentFailed message, long outboxMessageId, CancellationToken ct = default)
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

                var order = await _orderRepo.GetByIdAsync(message.OrderId, ct);
                if (order is null)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                var failedItems = message.Items
                    .Select(i => new FailedShipmentItem(i.ProductId, i.Quantity))
                    .ToList();
                order.RecordShipmentFailure(message.ShipmentId, failedItems);
                await _orderRepo.UpdateAsync(order, ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
