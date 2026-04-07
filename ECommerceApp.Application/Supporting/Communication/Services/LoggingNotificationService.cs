using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Services
{
    internal sealed class LoggingNotificationService : INotificationService
    {
        private readonly ILogger<LoggingNotificationService> _logger;

        public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyAsync(string userId, string eventType, string message, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[Communication] Push to user {UserId} — {EventType}: {Message}",
                userId, eventType, message);
            return Task.CompletedTask;
        }
    }
}
