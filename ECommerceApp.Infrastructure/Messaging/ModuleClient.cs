using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class ModuleClient : IModuleClient
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ModuleClient> _logger;

        public ModuleClient(IServiceProvider serviceProvider, ILogger<ModuleClient> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task PublishAsync(IMessage message)
        {
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
            var handler = _serviceProvider.GetService(handlerType);
            if (handler is null)
            {
                _logger.LogWarning("No handler registered for message type {MessageType}", message.GetType().Name);
                return;
            }

            await ((dynamic)handler).HandleAsync((dynamic)message);
        }
    }
}
