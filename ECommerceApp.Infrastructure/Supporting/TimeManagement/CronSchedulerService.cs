using Cronos;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class CronSchedulerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly JobTriggerChannel _channel;
        private readonly IJobStatusMonitor _monitor;
        private readonly ILogger<CronSchedulerService> _logger;

        public CronSchedulerService(
            IServiceScopeFactory scopeFactory,
            JobTriggerChannel channel,
            IJobStatusMonitor monitor,
            ILogger<CronSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _channel = channel;
            _monitor = monitor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // A5: align first tick to the next 30-second clock boundary
            await Task.Delay(ComputeAlignmentDelay(), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await TickAsync(stoppingToken);
                await Task.Delay(ComputeAlignmentDelay(), stoppingToken);
            }
        }

        private static int ComputeAlignmentDelay()
        {
            var now = DateTime.UtcNow;
            return (30 - now.Second % 30) * 1000 - now.Millisecond;
        }

        private async Task TickAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
                var jobs = await repo.GetEnabledAsync(ct);

                foreach (var job in jobs)
                {
                    try
                    {
                        var lastRun = _monitor.GetLatest(job.Name.Value)?.CompletedAt
                                      ?? job.LastRunAt
                                      ?? now.AddSeconds(-30);

                        var cron = CronExpression.Parse(job.Schedule);
                        var tz = string.IsNullOrEmpty(job.TimeZoneId)
                            ? TimeZoneInfo.Utc
                            : TimeZoneInfo.FindSystemTimeZoneById(job.TimeZoneId);

                        var next = cron.GetNextOccurrence(lastRun, tz);
                        if (next.HasValue && next.Value <= now)
                        {
                            await _channel.WriteAsync(new JobTriggerRequest
                            {
                                JobName = job.Name.Value,
                                Source = JobTriggerSource.Scheduled
                            }, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CronSchedulerService error for job '{JobName}'", job.Name.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CronSchedulerService failed to read enabled jobs");
            }
        }
    }
}
