using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class BackgroundMessageDispatcher : BackgroundService
    {
        private readonly IMessageChannel _channel;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BackgroundMessageDispatcher> _logger;

        public BackgroundMessageDispatcher(
            IMessageChannel channel,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<BackgroundMessageDispatcher> logger)
        {
            _channel = channel;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(ct))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                try
                {
                    var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
                    var handler = scope.ServiceProvider.GetService(handlerType);
                    if (handler is null)
                    {
                        _logger.LogWarning("No handler registered for message type {MessageType}", message.GetType().Name);
                        continue;
                    }

                    await ((dynamic)handler).HandleAsync((dynamic)message, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message of type {MessageType}", message.GetType().Name);
                }
            }
        }
    }
}
