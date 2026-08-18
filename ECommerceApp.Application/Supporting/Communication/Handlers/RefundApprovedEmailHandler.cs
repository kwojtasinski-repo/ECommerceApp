using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class RefundApprovedEmailHandler : IIdAwareMessageHandler<RefundApproved>
    {
        private readonly IEmailService _emails;
        private readonly IOrderUserResolver _userResolver;
        private readonly IUserEmailResolver _emailResolver;
        private readonly IProcessedMessageGuard _processedMessageGuard;
        private readonly IOutboxWriter _outboxWriter;

        public RefundApprovedEmailHandler(
            IEmailService emails,
            IOrderUserResolver userResolver,
            IUserEmailResolver emailResolver,
            IProcessedMessageGuard processedMessageGuard,
            IOutboxWriter outboxWriter)
        {
            _emails = emails;
            _userResolver = userResolver;
            _emailResolver = emailResolver;
            _processedMessageGuard = processedMessageGuard;
            _outboxWriter = outboxWriter;
        }

        public async Task HandleAsync(RefundApproved message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(RefundApproved message, long outboxMessageId, CancellationToken ct = default)
        {
            var handlerType = GetType().FullName
                ?? throw new InvalidOperationException("Handler type name is unavailable.");
            if (!await _processedMessageGuard.TryMarkProcessedAsync(outboxMessageId, handlerType, ct))
            {
                return;
            }

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
                Subject: $"Zwrot #{message.RefundId} zatwierdzony",
                Body: $"Twój zwrot #{message.RefundId} dla zamówienia #{message.OrderId} " +
                      $"został zatwierdzony dnia {message.OccurredAt:d}.",
                Actions: new[] { new EmailAction("Szczegóły zwrotu", $"/sales/refunds/{message.RefundId}") }
            ), ct);

            await _outboxWriter.EnqueueAsync(new RefundCustomerNotified(message.RefundId), ct);
        }
    }
}
