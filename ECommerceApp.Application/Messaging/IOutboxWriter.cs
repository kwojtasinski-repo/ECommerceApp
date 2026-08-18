using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IOutboxWriter
    {
        Task EnqueueAsync(IMessage message, CancellationToken ct = default);

        /// <summary>
        /// Enqueues <paramref name="message"/> into the Outbox as part of the already-open
        /// <paramref name="transaction"/>. Does not commit — the caller commits once, after all its
        /// aggregate writes and Outbox enqueues are done, via <c>transaction.CommitAsync()</c>.
        /// </summary>
        Task EnqueueAsync(IMessage message, IOutboxTransaction transaction, CancellationToken ct = default);
    }
}
