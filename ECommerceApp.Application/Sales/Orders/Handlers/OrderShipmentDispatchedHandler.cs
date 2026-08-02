using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderShipmentDispatchedHandler : IIdAwareMessageHandler<ShipmentDispatched>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IOrdersUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderShipmentDispatchedHandler(
            IOrderRepository orderRepo,
            IOrdersUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _orderRepo = orderRepo;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(ShipmentDispatched message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(ShipmentDispatched message, long outboxMessageId, CancellationToken ct = default)
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

                order.RecordShipmentDispatched();
                await _orderRepo.UpdateAsync(order, ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
