using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentExpiredNotificationHandler : IMessageHandler<PaymentExpired>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;
        private readonly ILogger<PaymentExpiredNotificationHandler> _logger;

        public PaymentExpiredNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver,
            ILogger<PaymentExpiredNotificationHandler> logger)
        {
            _notifications = notifications;
            _userResolver = userResolver;
            _logger = logger;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
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
