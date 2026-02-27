using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class DeferredJobPollerService : BackgroundService
    {
        private static readonly TimeSpan LockWindow = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly JobTriggerChannel _channel;
        private readonly ILogger<DeferredJobPollerService> _logger;

        public DeferredJobPollerService(
            IServiceScopeFactory scopeFactory,
            JobTriggerChannel channel,
            ILogger<DeferredJobPollerService> logger)
        {
            _scopeFactory = scopeFactory;
            _channel = channel;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                await PollAsync(stoppingToken);
            }
        }

        private async Task PollAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TimeManagementDbContext>();
                var now = DateTime.UtcNow;

                // A4: fetch both due-pending rows and zombie rows (Running with expired lock)
                var candidates = await context.DeferredJobQueue
                    .Where(d => (d.Status == DeferredJobStatus.Pending && d.RunAt <= now)
                             || (d.Status == DeferredJobStatus.Running && d.LockExpiresAt < now))
                    .OrderBy(d => d.RunAt)
                    .Take(50)
                    .ToListAsync(ct);

                foreach (var instance in candidates)
                {
                    // A4: zombie recovery — reset to Pending, do NOT dispatch immediately
                    if (instance.Status == DeferredJobStatus.Running)
                    {
                        _logger.LogWarning(
                            "Zombie detected for job '{JobName}' (id={Id}), resetting to Pending",
                            instance.JobName, instance.Id.Value);
                        instance.ResetZombie(now);
                        await context.SaveChangesAsync(ct);
                        continue;
                    }

                    instance.MarkRunning(now + LockWindow);
                    await context.SaveChangesAsync(ct);

                    await _channel.WriteAsync(new JobTriggerRequest
                    {
                        JobName = instance.JobName,
                        EntityId = instance.EntityId,
                        Source = JobTriggerSource.Deferred,
                        DeferredInstanceId = instance.Id.Value
                    }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeferredJobPollerService error");
            }
        }
    }
}
