using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderPlacedEmailHandler : IIdAwareMessageHandler<OrderPlaced>
    {
        private readonly IEmailService _emails;
        private readonly IUserEmailResolver _emailResolver;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderPlacedEmailHandler(
            IEmailService emails,
            IUserEmailResolver emailResolver,
            IProcessedMessageGuard processedMessageGuard)
        {
            _emails = emails;
            _emailResolver = emailResolver;
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
