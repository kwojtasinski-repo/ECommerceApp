using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class RefundRejectedNotificationHandler : IMessageHandler<RefundRejected>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;

        public RefundRejectedNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver)
        {
            _notifications = notifications;
            _userResolver = userResolver;
        }

        public async Task HandleAsync(RefundRejected message, CancellationToken ct = default)
        {
            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            await _notifications.NotifyAsync(
                userId,
                "RefundRejected",
                $"Twój zwrot #{message.RefundId} dla zamówienia #{message.OrderId} " +
                $"został odrzucony dnia {message.OccurredAt:d}.",
                ct);
        }
    }
}
