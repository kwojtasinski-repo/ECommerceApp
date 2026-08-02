using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderPlacedNotificationHandler : IIdAwareMessageHandler<OrderPlaced>
    {
        private readonly INotificationService _notifications;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderPlacedNotificationHandler(
            INotificationService notifications,
            IProcessedMessageGuard processedMessageGuard)
        {
            _notifications = notifications;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderPlaced message, long outboxMessageId, CancellationToken ct = default)
        {
            var handlerType = GetType().FullName
                ?? throw new InvalidOperationException("Handler type name is unavailable.");
            if (!await _processedMessageGuard.TryMarkProcessedAsync(outboxMessageId, handlerType, ct))
            {
                return;
            }

            await _notifications.NotifyAsync(
                message.UserId,
                "OrderPlaced",
                $"Twoje zamówienie #{message.OrderId} zostało przyjęte. " +
                $"Łączna kwota: {message.TotalAmount:0.00}. " +
                $"Płatność przyjmowana do: {message.ExpiresAt:g}.",
                ct);
        }
    }
}
