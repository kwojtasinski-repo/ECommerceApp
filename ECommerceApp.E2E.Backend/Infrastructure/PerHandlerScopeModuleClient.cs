using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// Real-SQL-Server E2E test double for <see cref="IModuleClient"/>. Unlike production's
    /// <c>ModuleClient</c>, which resolves and invokes every registered handler against one shared
    /// ambient DI scope, this dispatches each handler in its own freshly created scope, so each
    /// handler gets its own <c>DbContext</c> instance instead of sharing one across the dispatch.
    /// Needed because these tests run the real background outbox dispatcher against a real SQL
    /// Server engine, where handlers reacting to the same message can each open their own unit of
    /// work; sharing a single ambient scope's <c>DbContext</c> across them risks exactly the kind
    /// of same-context nested-transaction conflict fixed by the <c>StockService</c>
    /// transaction-accepting overloads (see the commit that introduced this class).
    /// </summary>
    internal sealed class PerHandlerScopeModuleClient : IModuleClient
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public PerHandlerScopeModuleClient(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task PublishAsync(IMessage message, long? outboxMessageId = null)
        {
            var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
            var idAwareHandlerType = typeof(IIdAwareMessageHandler<>).MakeGenericType(message.GetType());

            using var probe = _scopeFactory.CreateScope();
            var handlers = probe.ServiceProvider.GetServices(handlerType).ToArray();
            foreach (var handler in handlers)
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedHandler = scope.ServiceProvider.GetServices(handlerType)
                    .Single(candidate => candidate.GetType() == handler.GetType());
                var isIdAware = outboxMessageId.HasValue && idAwareHandlerType.IsInstanceOfType(scopedHandler);
                var method = isIdAware
                    ? idAwareHandlerType.GetMethod(nameof(IIdAwareMessageHandler<IMessage>.HandleAsync))
                    : handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                if (method is null)
                {
                    continue;
                }

                var arguments = isIdAware
                    ? new object[] { message, outboxMessageId!.Value, CancellationToken.None }
                    : new object[] { message, CancellationToken.None };
                await (Task)method.Invoke(scopedHandler, arguments)!;
            }
        }

        public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
            => throw new NotSupportedException("Queries are not used by the real-SQL-Server E2E module client.");
    }
}
