using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
                try
                {
                    var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
                    var method = handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                    if (method is null)
                    {
                        _logger.LogError("No HandleAsync method on handler type for {MessageType}", message.GetType().Name);
                        continue;
                    }

                    int handlerCount;
                    using (var probe = _serviceScopeFactory.CreateScope())
                    {
                        handlerCount = probe.ServiceProvider.GetServices(handlerType).Count();
                    }

                    if (handlerCount == 0)
                    {
                        _logger.LogWarning("No handler registered for message type {MessageType}", message.GetType().Name);
                        continue;
                    }

                    var tasks = new List<Task>(handlerCount);
                    for (var i = 0; i < handlerCount; i++)
                    {
                        tasks.Add(RunInScopeAsync(i));
                    }

                    await Task.WhenAll(tasks);

                    async Task RunInScopeAsync(int index)
                    {
                        using var handlerScope = _serviceScopeFactory.CreateScope();
                        var h = handlerScope.ServiceProvider.GetServices(handlerType).ElementAt(index);
                        await (Task)method.Invoke(h, new object[] { message, ct })!;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message of type {MessageType}", message.GetType().Name);
                }
            }
        }
    }
}
