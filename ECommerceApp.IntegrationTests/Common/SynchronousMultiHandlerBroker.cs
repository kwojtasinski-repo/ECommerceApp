using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    /// <summary>
    /// Test-only <see cref="IMessageBroker"/> that dispatches synchronously to ALL
    /// registered <see cref="IMessageHandler{T}"/> instances for each message.
    ///
    /// In production, <c>BackgroundMessageDispatcher</c> does this via <c>GetServices</c>
    /// + <c>Channel&lt;T&gt;</c> (async). <c>ModuleClient</c> (sync) uses <c>GetService</c>
    /// (singular) and misses multi-consumer events.
    ///
    /// This broker eliminates both problems for integration tests:
    /// 1. Dispatches to ALL handlers (uses <c>GetServices</c>)
    /// 2. Runs synchronously — assertions safe immediately after <c>PublishAsync</c>
    /// 3. Supports recursive publishing (handler A publishes message B → B handlers run inline)
    /// </summary>
    internal sealed class SynchronousMultiHandlerBroker : IMessageBroker
    {
        private readonly IServiceProvider _serviceProvider;

        public SynchronousMultiHandlerBroker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync(params IMessage[] messages)
        {
            if (messages is null)
                return;

            foreach (var message in messages.Where(m => m is not null))
            {
                var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
                var handlers = _serviceProvider.GetServices(handlerType).ToList();

                foreach (var handler in handlers)
                {
                    var method = handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                    if (method is null)
                        continue;

                    await (Task)method.Invoke(handler, new object[] { message, default(System.Threading.CancellationToken) })!;
                }
            }
        }
    }
}
