using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class OutboxWriter : IOutboxWriter
    {
        private readonly MessagingDbContext _messagingContext;
        private readonly OutboxWakeSignal _wakeSignal;

        public OutboxWriter(MessagingDbContext messagingContext, OutboxWakeSignal wakeSignal)
        {
            _messagingContext = messagingContext;
            _wakeSignal = wakeSignal;
        }

        public async Task EnqueueAsync(IMessage message, CancellationToken ct = default)
        {
            await using var scope = await CrossContextTransactionScope.BeginAsync(
                _messagingContext,
                ct: ct);

            await EnqueueAsync(message, _messagingContext, ct);
            await scope.CommitAsync(ct);
            _wakeSignal.Signal();
        }

        public async Task EnqueueAsync(IMessage message, IOutboxTransaction transaction, CancellationToken ct = default)
        {
            if (transaction is not OutboxTransaction concrete)
            {
                throw new InvalidOperationException(
                    $"{nameof(IOutboxTransaction)} passed to {nameof(OutboxWriter)}.{nameof(EnqueueAsync)} must have " +
                    $"been created by a BC's unit-of-work (e.g. ICatalogUnitOfWork.BeginTransactionAsync), got " +
                    $"'{transaction.GetType().FullName}'.");
            }

            var key = MessageTypeRegistry.KeyFor(message.GetType());
            var payload = JsonSerializer.Serialize(message, message.GetType());
            var outboxMessage = OutboxMessage.Create(key, payload);

            await using var messagingContext = concrete.Scope.CreateSecondaryContext<MessagingDbContext>();
            messagingContext.Outbox.Add(outboxMessage);
            await messagingContext.SaveChangesAsync(ct);

            // The caller commits the ambient transaction after this returns, so this row isn't durable
            // yet — a near-immediate wake is still worth it (the caller's own commit is typically the
            // very next statement) and the poller's periodic timer covers the rare case where the wake
            // fires before the commit lands.
            _wakeSignal.Signal();
        }

        private static async Task EnqueueAsync(
            IMessage message,
            MessagingDbContext messagingContext,
            CancellationToken ct)
        {
            var key = MessageTypeRegistry.KeyFor(message.GetType());
            var payload = JsonSerializer.Serialize(message, message.GetType());
            messagingContext.Outbox.Add(OutboxMessage.Create(key, payload));
            await messagingContext.SaveChangesAsync(ct);
        }
    }
}
