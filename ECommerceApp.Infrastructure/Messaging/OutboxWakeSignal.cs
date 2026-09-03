using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    /// <summary>
    /// Lets <see cref="OutboxWriter"/> wake <see cref="OutboxPollerService"/> as soon as a message is
    /// enqueued, instead of it sitting idle for up to <see cref="Application.Messaging.MessagingOptions.OutboxPollInterval"/>.
    /// Multiple signals before the poller wakes coalesce into a single early tick — this is a latency
    /// hint, not a delivery guarantee, so the poller's own periodic timer stays as the fallback.
    /// </summary>
    internal sealed class OutboxWakeSignal
    {
        private readonly SemaphoreSlim _semaphore = new(0, 1);

        public void Signal()
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // A wake-up is already queued; nothing more to do.
            }
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
    }
}
