using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentExpiredNotificationHandler : IIdAwareMessageHandler<PaymentExpired>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;
        private readonly ILogger<PaymentExpiredNotificationHandler> _logger;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public PaymentExpiredNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver,
            ILogger<PaymentExpiredNotificationHandler> logger,
            IProcessedMessageGuard processedMessageGuard)
        {
            _notifications = notifications;
            _userResolver = userResolver;
            _logger = logger;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(PaymentExpired message, long outboxMessageId, CancellationToken ct = default)
        {
            var handlerType = GetType().FullName
                ?? throw new InvalidOperationException("Handler type name is unavailable.");
            if (!await _processedMessageGuard.TryMarkProcessedAsync(outboxMessageId, handlerType, ct))
            {
                return;
            }

            _logger.LogInformation(
                "[Communication][PaymentExpiredNotificationHandler] Received PaymentExpired. PaymentId={PaymentId} OrderId={OrderId} CorrelationId={CorrelationId}",
                message.PaymentId, message.OrderId, message.CorrelationId);

            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            await _notifications.NotifyAsync(
                userId,
                "PaymentExpired",
                $"Okno płatności #{message.PaymentId} dla zamówienia #{message.OrderId} " +
                $"wygasło dnia {message.OccurredAt:d}.",
                ct);
        }
    }
}
