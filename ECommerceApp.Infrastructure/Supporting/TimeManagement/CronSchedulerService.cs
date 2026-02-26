using Cronos;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class CronSchedulerService : BackgroundService
    {
        private readonly IEnumerable<IScheduleConfig> _configs;
        private readonly JobTriggerChannel _channel;
        private readonly IJobStatusMonitor _monitor;
        private readonly ILogger<CronSchedulerService> _logger;
        private readonly DateTime _startedAt;

        public CronSchedulerService(
            IEnumerable<IScheduleConfig> configs,
            JobTriggerChannel channel,
            IJobStatusMonitor monitor,
            ILogger<CronSchedulerService> logger)
        {
            _configs = configs;
            _channel = channel;
            _monitor = monitor;
            _logger = logger;
            _startedAt = DateTime.UtcNow;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await TickAsync(stoppingToken);
            }
        }

        private async Task TickAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            foreach (var config in _configs)
            {
                if (config.JobType != JobType.Recurring || config.CronExpression == null)
                    continue;

                try
                {
                    var lastRun = _monitor.GetLatest(config.JobName)?.CompletedAt;
                    var referenceTime = lastRun ?? _startedAt;

                    var cron = CronExpression.Parse(config.CronExpression);
                    var tz = string.IsNullOrEmpty(config.TimeZoneId)
                        ? TimeZoneInfo.Utc
                        : TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);

                    var next = cron.GetNextOccurrence(referenceTime, tz);
                    if (next.HasValue && next.Value <= now)
                    {
                        await _channel.WriteAsync(new JobTriggerRequest
                        {
                            JobName = config.JobName,
                            Source = JobTriggerSource.Scheduled
                        }, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CronSchedulerService error for job '{JobName}'", config.JobName);
                }
            }
        }
    }
}
