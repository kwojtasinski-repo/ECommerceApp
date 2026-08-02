using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderCancelledEmailHandler : IIdAwareMessageHandler<OrderCancelled>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderCancelledEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver,
            IProcessedMessageGuard processedMessageGuard)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(OrderCancelled message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderCancelled message, long outboxMessageId, CancellationToken ct = default)
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

            var toEmail = await _emailResolver.GetEmailForUserAsync(userId, ct);
            if (toEmail is null)
                return;

            await _emails.SendAsync(new EmailTemplate(
                To: toEmail,
                Subject: $"Zamówienie #{message.OrderId} zostało anulowane",
                Body: $"Twoje zamówienie #{message.OrderId} zostało anulowane dnia {message.OccurredAt:d}.",
                Actions: new[] { new EmailAction("Moje zamówienia", "/sales/orders/my") }
            ), ct);
        }
    }
}
