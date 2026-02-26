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

                var pending = await context.DeferredJobInstances
                    .Include(d => d.ScheduledJob)
                    .Where(d => d.Status == DeferredJobStatus.Pending && d.RunAt <= now)
                    .OrderBy(d => d.RunAt)
                    .Take(50)
                    .ToListAsync(ct);

                foreach (var instance in pending)
                {
                    if (instance.ScheduledJob == null)
                        continue;

                    instance.MarkRunning();
                    await context.SaveChangesAsync(ct);

                    await _channel.WriteAsync(new JobTriggerRequest
                    {
                        JobName = instance.ScheduledJob.Name.Value,
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
