using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderCancelledNotificationHandler : IMessageHandler<OrderCancelled>
    {
        private readonly INotificationService _notifications;
        private readonly IOrderUserResolver _userResolver;

        public OrderCancelledNotificationHandler(
            INotificationService notifications,
            IOrderUserResolver userResolver)
        {
            _notifications = notifications;
            _userResolver = userResolver;
        }

        public async Task HandleAsync(OrderCancelled message, CancellationToken ct = default)
        {
            var userId = await _userResolver.GetUserIdForOrderAsync(message.OrderId, ct);
            if (userId is null)
                return;

            await _notifications.NotifyAsync(
                userId,
                "OrderCancelled",
                $"Twoje zamówienie #{message.OrderId} zostało anulowane dnia {message.OccurredAt:d}.",
                ct);
        }
    }
}
