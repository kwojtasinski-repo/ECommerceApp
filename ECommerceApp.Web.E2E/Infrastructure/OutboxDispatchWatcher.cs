using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    public sealed class OutboxDispatchWatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _pollInterval;

        public OutboxDispatchWatcher(IServiceScopeFactory scopeFactory, TimeSpan? pollInterval = null)
        {
            _scopeFactory = scopeFactory;
            _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        }

        public async Task WaitForDispatchedAsync<TMessage>(
            DateTime sinceUtc,
            Func<TMessage, bool> predicate,
            TimeSpan timeout,
            CancellationToken ct = default)
            where TMessage : IMessage
        {
            var deadline = DateTime.UtcNow + timeout;
            var candidates = new List<OutboxMessage>();

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var messages = await repository.GetSinceAsync(sinceUtc, 100, ct);

                foreach (var message in messages.Where(m => MessageTypeRegistry.TypeFor(m.MessageTypeKey) == typeof(TMessage)))
                {
                    if (candidates.All(candidate => candidate.Id != message.Id))
                    {
                        candidates.Add(message);
                    }

                    var deserialized = JsonSerializer.Deserialize<TMessage>(message.Payload);
                    if (deserialized != null && predicate(deserialized) && message.Status == OutboxStatus.Dispatched)
                    {
                        return;
                    }
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, ct);
            }

            var details = candidates.Count == 0
                ? "none"
                : string.Join(", ", candidates.Select(m => $"Id={m.Id}, Status={m.Status}"));
            throw new TimeoutException(
                $"No matching dispatched {typeof(TMessage).Name} message was observed before the timeout. "
                + $"Candidate count: {candidates.Count}. Candidates: {details}.");
        }
    }
}
