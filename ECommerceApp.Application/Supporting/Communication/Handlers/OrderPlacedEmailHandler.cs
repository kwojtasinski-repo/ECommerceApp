using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderPlacedEmailHandler : IMessageHandler<OrderPlaced>
    {
        private readonly IEmailService _emails;
        private readonly IUserEmailResolver _emailResolver;

        public OrderPlacedEmailHandler(IEmailService emails, IUserEmailResolver emailResolver)
        {
            _emails = emails;
            _emailResolver = emailResolver;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            var toEmail = await _emailResolver.GetEmailForUserAsync(message.UserId, ct);
            if (toEmail is null)
                return;

            await _emails.SendAsync(new EmailTemplate(
                To: toEmail,
                Subject: $"Potwierdzenie zamówienia #{message.OrderId}",
                Body: $"Twoje zamówienie #{message.OrderId} zostało przyjęte. " +
                      $"Łączna kwota: {message.TotalAmount:0.00}. " +
                      $"Opłać zamówienie do: {message.ExpiresAt:g}.",
                Actions: new[] { new EmailAction("Zobacz zamówienie", $"/sales/orders/{message.OrderId}") }
            ), ct);
        }
    }
}
