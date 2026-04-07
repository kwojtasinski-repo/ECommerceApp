using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class PaymentExpiredNotificationHandler : IMessageHandler<PaymentExpired>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;

        public PaymentExpiredNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver)
        {
            _notifications = notifications;
            _userResolver = userResolver;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
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
