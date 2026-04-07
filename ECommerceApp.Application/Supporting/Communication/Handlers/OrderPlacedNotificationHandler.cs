using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    internal sealed class OrderPlacedNotificationHandler : IMessageHandler<OrderPlaced>
    {
        private readonly INotificationService _notifications;

        public OrderPlacedNotificationHandler(INotificationService notifications)
        {
            _notifications = notifications;
        }

        public Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
            => _notifications.NotifyAsync(
                message.UserId,
                "OrderPlaced",
                $"Twoje zamówienie #{message.OrderId} zostało przyjęte. " +
                $"Łączna kwota: {message.TotalAmount:0.00}. " +
                $"Płatność przyjmowana do: {message.ExpiresAt:g}.",
                ct);
    }
}
