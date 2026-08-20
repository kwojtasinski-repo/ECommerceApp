using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
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

        public OutboxPollerService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxPollerService> logger,
            MessagingOptions messagingOptions)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _messagingOptions = messagingOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(_messagingOptions.OutboxPollInterval, stoppingToken);
                    await PollAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task PollAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<ECommerceApp.Application.Constants.RetryPolicyOptions>>();
                var now = DateTime.UtcNow;

                var candidates = await outboxRepository.GetDueAsync(batchSize: 50, ct);

                foreach (var message in candidates)
                {
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
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPollerService error");
            }
        }
    }
}
