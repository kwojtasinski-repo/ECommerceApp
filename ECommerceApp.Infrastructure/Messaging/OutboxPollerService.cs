using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class OutboxPollerService : BackgroundService
    {
        private static readonly TimeSpan LockWindow = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxPollerService> _logger;
        private readonly MessagingOptions _messagingOptions;
        private readonly OutboxWakeSignal _wakeSignal;

        public OutboxPollerService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxPollerService> logger,
            MessagingOptions messagingOptions,
            OutboxWakeSignal wakeSignal)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _messagingOptions = messagingOptions;
            _wakeSignal = wakeSignal;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Waits for either a writer's wake-up signal or the periodic interval, whichever
                    // comes first — closes the perceived latency between "message enqueued" and "message
                    // dispatched" without giving up the timer as a fallback (zombie recovery, missed
                    // wake-ups, etc. all still rely on it firing regardless of signals).
                    await _wakeSignal.WaitAsync(_messagingOptions.OutboxPollInterval, stoppingToken);
                    await PollAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task PollAsync(CancellationToken ct)
        {
            IReadOnlyList<OutboxMessage> candidates;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                candidates = await outboxRepository.GetDueAsync(batchSize: 50, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPollerService error fetching due messages");
                return;
            }

            // Each message gets its own scope and its own try/catch: a concurrency conflict (another
            // poller instance claimed the same row first — expected when multiple hosts share one
            // Outbox table, as the E2E suite does) must not abort the rest of the batch. Previously one
            // failing message stopped every message behind it in the same 50-row poll.
            foreach (var message in candidates)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    var options = scope.ServiceProvider.GetRequiredService<IOptions<ECommerceApp.Application.Constants.RetryPolicyOptions>>();
                    var now = DateTime.UtcNow;

                    if (message.Status == OutboxStatus.Running)
                    {
                        _logger.LogWarning(
                            "Zombie detected for outbox message (id={Id}), resetting to Pending",
                            message.Id);
                        message.ResetZombie(now, options.Value.MaxBackoff);
                        await outboxRepository.UpdateAsync(message, ct);
                        continue;
                    }

                    message.MarkRunning(now + LockWindow);
                    await outboxRepository.UpdateAsync(message, ct);

                    var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                    await dispatcher.DispatchAsync(message, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxPollerService error dispatching message (id={Id})", message.Id);
                }
            }
        }
    }
}
