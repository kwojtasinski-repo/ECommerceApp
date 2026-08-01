using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
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

        public async Task PublishAsync(IMessage message, long? outboxMessageId = null)
        {
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
            var idAwareHandlerType = typeof(IIdAwareMessageHandler<>).MakeGenericType(message.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);
            var dispatched = false;

            foreach (var handler in handlers)
            {
                var method = outboxMessageId.HasValue && idAwareHandlerType.IsInstanceOfType(handler)
                    ? idAwareHandlerType.GetMethod(nameof(IIdAwareMessageHandler<IMessage>.HandleAsync))
                    : handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                if (method is null)
                    continue;

                var arguments = outboxMessageId.HasValue && idAwareHandlerType.IsInstanceOfType(handler)
                    ? new object[] { message, outboxMessageId.Value, default(CancellationToken) }
                    : new object[] { message, default(CancellationToken) };
                await (Task)method.Invoke(handler, arguments)!;
                dispatched = true;
            }

            if (!dispatched)
            {
                _logger.LogWarning("No handler registered for message type {MessageType}", message.GetType().Name);
            }
        }

        public async Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            var handler = _serviceProvider.GetService(handlerType)
                ?? throw new InvalidOperationException(
                    $"No query handler registered for {query.GetType().Name}. " +
                    $"Register an IQueryHandler<{query.GetType().Name}, {typeof(TResult).Name}> in DI.");
            return await ((dynamic)handler).HandleAsync((dynamic)query, ct);
        }
    }
}
