using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
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
    ///
    /// Mirrors <c>ModuleClient</c>'s id-aware dispatch (Phase 4/Inbox idempotency): a handler
    /// that also implements <see cref="IIdAwareMessageHandler{TMessage}"/> is invoked with an
    /// <c>outboxMessageId</c> so dedup-wrapped handlers don't hit their `NotSupportedException`
    /// fallback. <see cref="PublishAsync"/> auto-generates a fresh, unique id per call so distinct
    /// business events across tests are never mistaken for a redelivery of each other;
    /// <see cref="RedeliverAsync"/> lets a test pass the same id twice to deliberately simulate one.
    /// </summary>
    public sealed class SynchronousMultiHandlerBroker : IMessageBroker
    {
        private readonly IServiceProvider _serviceProvider;
        private static long _syntheticIdSeed;

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
                await DispatchAsync(message, Interlocked.Increment(ref _syntheticIdSeed), CancellationToken.None);
            }
        }

        public Task RedeliverAsync(IMessage message, long outboxMessageId, CancellationToken ct = default)
            => DispatchAsync(message, outboxMessageId, ct);

        private async Task DispatchAsync(IMessage message, long outboxMessageId, CancellationToken ct)
        {
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
            var idAwareHandlerType = typeof(IIdAwareMessageHandler<>).MakeGenericType(message.GetType());
            var handlers = _serviceProvider.GetServices(handlerType).ToList();

            foreach (var handler in handlers)
            {
                var isIdAware = idAwareHandlerType.IsInstanceOfType(handler);
                var method = isIdAware
                    ? idAwareHandlerType.GetMethod(nameof(IIdAwareMessageHandler<IMessage>.HandleAsync))
                    : handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                if (method is null)
                    continue;

                var arguments = isIdAware
                    ? new object[] { message, outboxMessageId, ct }
                    : new object[] { message, ct };
                await (Task)method.Invoke(handler, arguments)!;
            }
        }
    }
}
