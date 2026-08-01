using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IProcessedMessageGuard
    {
        Task<bool> TryMarkProcessedAsync(
            long messageId,
            string handlerType,
            IOutboxTransaction transaction,
            CancellationToken ct = default);

        /// <summary>
        /// Overload for handlers with no local DB write of their own to commit alongside the guard
        /// (e.g. Supporting/Communication's email/notification handlers — no BC DbContext to anchor a
        /// <see cref="IOutboxTransaction"/> on). Opens its own transaction directly against
        /// <c>MessagingDbContext</c>. Ordering tradeoff: the marker is written *before* the handler's
        /// side effect runs, so a crash between the two would skip a legitimate send on the next
        /// redelivery — accepted deliberately, since the alternative (mark-after) reopens the exact
        /// duplicate-send window this guard exists to close.
        /// </summary>
        Task<bool> TryMarkProcessedAsync(
            long messageId,
            string handlerType,
            CancellationToken ct = default);
    }
}