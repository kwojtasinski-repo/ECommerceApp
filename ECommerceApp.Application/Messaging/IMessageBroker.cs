using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IMessageBroker
    {
        Task PublishAsync(params IMessage[] messages);

        /// <summary>
        /// Redelivers a single message under an explicit, caller-chosen <c>OutboxMessage.Id</c> —
        /// for tests simulating an at-least-once redelivery of the same Outbox row (dedup testing).
        /// Not used by production code: real redelivery always goes through the Outbox/OutboxDispatcher.
        /// </summary>
        Task RedeliverAsync(IMessage message, long outboxMessageId, CancellationToken ct = default);
    }
}
