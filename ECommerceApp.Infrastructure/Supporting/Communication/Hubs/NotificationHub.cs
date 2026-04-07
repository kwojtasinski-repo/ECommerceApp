using Microsoft.AspNetCore.SignalR;

namespace ECommerceApp.Infrastructure.Supporting.Communication.Hubs
{
    /// <summary>
    /// Push-only hub. Clients subscribe to "ReceiveNotification" to receive real-time
    /// domain event notifications (order status, payment, refund outcomes).
    /// No client-to-server methods — this is a server-push channel only.
    /// </summary>
    public sealed class NotificationHub : Hub { }
}
