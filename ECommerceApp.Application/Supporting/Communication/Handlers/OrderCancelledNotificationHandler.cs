using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderCancelledNotificationHandler : IIdAwareMessageHandler<OrderCancelled>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderCancelledNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver,
            IProcessedMessageGuard processedMessageGuard)
        {
            _notifications = notifications;
            _userResolver = userResolver;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(OrderCancelled message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderCancelled message, long outboxMessageId, CancellationToken ct = default)
        {
            var handlerType = GetType().FullName
                ?? throw new InvalidOperationException("Handler type name is unavailable.");
            if (!await _processedMessageGuard.TryMarkProcessedAsync(outboxMessageId, handlerType, ct))
            {
                return;
            }

            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            await _notifications.NotifyAsync(
                userId,
                "OrderCancelled",
                $"Twoje zamówienie #{message.OrderId} zostało anulowane dnia {message.OccurredAt:d}.",
                ct);
        }
    }
}
