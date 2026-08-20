using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public sealed class MessageProcessingOperationsFixture
    {
        private static readonly TimeSpan DefaultTimeout =
            TimeSpan.FromSeconds(20);

        private static readonly TimeSpan DefaultPollInterval =
            TimeSpan.FromMilliseconds(500);

        public async Task<TState> WaitUntilAsync<TState>(
            IMessageProcessingOperation<TState> operation,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout ?? DefaultTimeout;
            var deadline = DateTime.UtcNow + effectiveTimeout;
            TState lastState = default!;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastState = await operation.ReadAsync(cancellationToken);

                if (operation.IsCompleted(lastState))
                {
                    return lastState;
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(
                    remaining < DefaultPollInterval ? remaining : DefaultPollInterval,
                    cancellationToken);
            }

            throw new TimeoutException(operation.Describe(lastState));
        }

        public async Task<TState> ExecuteAndWaitUntilAsync<TState>(
            IStartableMessageProcessingOperation<TState> operation,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            await operation.StartAsync(cancellationToken);

            return await WaitUntilAsync(
                operation,
                timeout,
                cancellationToken);
        }
    }
}