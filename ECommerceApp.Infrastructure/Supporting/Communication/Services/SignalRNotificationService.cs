using ECommerceApp.Application.Supporting.Communication.Services;
using ECommerceApp.Infrastructure.Supporting.Communication.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Communication.Services
{
    internal sealed class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyAsync(string userId, string eventType, string message, CancellationToken ct = default)
            => _hubContext.Clients.User(userId).SendAsync(
                "ReceiveNotification",
                new { EventType = eventType, Message = message },
                cancellationToken: ct);
    }
}
