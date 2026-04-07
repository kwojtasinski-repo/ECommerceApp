using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentConfirmedEmailHandler : IMessageHandler<PaymentConfirmed>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;

        public PaymentConfirmedEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
        }

        public async Task HandleAsync(PaymentConfirmed message, CancellationToken ct = default)
        {
            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            var toEmail = await _emailResolver.GetEmailForUserAsync(userId, ct);
            if (toEmail is null)
                return;

            await _emails.SendAsync(new EmailTemplate(
                To: toEmail,
                Subject: $"Płatność za zamówienie #{message.OrderId} potwierdzona",
                Body: $"Płatność #{message.PaymentId} za zamówienie #{message.OrderId} " +
                      $"została potwierdzona dnia {message.OccurredAt:d}.",
                Actions: new[] { new EmailAction("Szczegóły płatności", $"/sales/payments/{message.PaymentId}") }
            ), ct);
        }
    }
}
