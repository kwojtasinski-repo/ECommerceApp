using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentExpiredEmailHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;
        private readonly ILogger<PaymentExpiredEmailHandler> _logger;

        public PaymentExpiredEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver,
            ILogger<PaymentExpiredEmailHandler> logger)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
            _logger = logger;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
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
