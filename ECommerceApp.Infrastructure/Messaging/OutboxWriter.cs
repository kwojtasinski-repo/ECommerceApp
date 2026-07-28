using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class OutboxWriter : IOutboxWriter
    {
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
        }
    }
}
