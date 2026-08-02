using ECommerceApp.Application.Supporting.Communication.Services;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Test-only <see cref="INotificationService"/> that counts calls instead of notifying anything —
    /// counterpart to <see cref="CountingEmailService"/> for Communication handlers' redelivery tests.
    /// </summary>
    public sealed class CountingNotificationService : INotificationService
    {
        private readonly ConcurrentBag<(string UserId, string EventType, string Message)> _notified = new();

        public int NotifyCount => _notified.Count;

        public Task NotifyAsync(string userId, string eventType, string message, CancellationToken ct = default)
        {
            _notified.Add((userId, eventType, message));
            return Task.CompletedTask;
        }
    }
}
