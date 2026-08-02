using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentExpiredEmailHandler : IIdAwareMessageHandler<PaymentExpired>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;
        private readonly ILogger<PaymentExpiredEmailHandler> _logger;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public PaymentExpiredEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver,
            ILogger<PaymentExpiredEmailHandler> logger,
            IProcessedMessageGuard processedMessageGuard)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
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
                "[Communication][PaymentExpiredEmailHandler] Received PaymentExpired. PaymentId={PaymentId} OrderId={OrderId} CorrelationId={CorrelationId}",
                message.PaymentId, message.OrderId, message.CorrelationId);

            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
            {
                return;
            }

            var toEmail = await _emailResolver.GetEmailForUserAsync(userId, ct);
            if (toEmail is null)
            {
                return;
            }

            await _emails.SendAsync(new EmailTemplate(
                To: toEmail,
                Subject: $"Okno płatności dla zamówienia #{message.OrderId} wygasło",
                Body: $"Okno płatności #{message.PaymentId} dla zamówienia #{message.OrderId} " +
                      $"wygasło dnia {message.OccurredAt:d}. Zamówienie zostało anulowane.",
                Actions: new[] { new EmailAction("Moje zamówienia", "/sales/orders/my") }
            ), ct);
        }
    }
}
