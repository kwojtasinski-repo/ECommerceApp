using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class RefundRejectedEmailHandler : IMessageHandler<RefundRejected>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;

        public RefundRejectedEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
        }

        public async Task HandleAsync(RefundRejected message, CancellationToken ct = default)
        {
            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            var toEmail = await _emailResolver.GetEmailForUserAsync(userId, ct);
            if (toEmail is null)
                return;

            await _emails.SendAsync(new EmailTemplate(
                To: toEmail,
                Subject: $"Zwrot #{message.RefundId} został odrzucony",
                Body: $"Twój zwrot #{message.RefundId} dla zamówienia #{message.OrderId} " +
                      $"został odrzucony dnia {message.OccurredAt:d}.",
                Actions: new[] { new EmailAction("Szczegóły zamówienia", $"/sales/orders/{message.OrderId}") }
            ), ct);
        }
    }
}
