using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderRefundRejectedHandler : IIdAwareMessageHandler<RefundRejected>
    {
        private readonly IOrderService _orders;
        private readonly IOrdersUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderRefundRejectedHandler(
            IOrderService orders,
            IOrdersUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _orders = orders;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(RefundRejected message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(RefundRejected message, long outboxMessageId, CancellationToken ct = default)
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

                await _orders.RemoveRefundByRefundIdAsync(message.RefundId, ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
