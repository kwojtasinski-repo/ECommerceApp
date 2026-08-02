using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentConfirmedNotificationHandler : IIdAwareMessageHandler<PaymentConfirmed>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public PaymentConfirmedNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver,
            IProcessedMessageGuard processedMessageGuard)
        {
            _notifications = notifications;
            _userResolver = userResolver;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(PaymentConfirmed message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(PaymentConfirmed message, long outboxMessageId, CancellationToken ct = default)
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
                "PaymentConfirmed",
                $"Płatność #{message.PaymentId} za zamówienie #{message.OrderId} " +
                $"została potwierdzona dnia {message.OccurredAt:d}.",
                ct);
        }
    }
}
