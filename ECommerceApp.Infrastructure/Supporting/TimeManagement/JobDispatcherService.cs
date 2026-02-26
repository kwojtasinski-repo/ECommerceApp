using Cronos;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class JobDispatcherService : BackgroundService
    {
        private readonly JobTriggerChannel _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly InMemoryJobStatusMonitor _monitor;
        private readonly IEnumerable<IScheduleConfig> _configs;
        private readonly ILogger<JobDispatcherService> _logger;

        public JobDispatcherService(
            JobTriggerChannel channel,
            IServiceScopeFactory scopeFactory,
            InMemoryJobStatusMonitor monitor,
            IEnumerable<IScheduleConfig> configs,
            ILogger<JobDispatcherService> logger)
        {
            _channel = channel;
            _scopeFactory = scopeFactory;
            _monitor = monitor;
            _configs = configs;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var trigger in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessTriggerAsync(trigger, stoppingToken);
            }
        }

        private async Task ProcessTriggerAsync(JobTriggerRequest trigger, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var tasks = scope.ServiceProvider.GetServices<IScheduledTask>();
            var task = tasks.FirstOrDefault(t => t.TaskName == trigger.JobName);

            if (task == null)
            {
                _logger.LogWarning("No IScheduledTask registered for job '{JobName}'", trigger.JobName);
                return;
            }

            var executionId = Guid.NewGuid().ToString();
            var context = new JobExecutionContext(trigger.EntityId, executionId);
            var startedAt = DateTime.UtcNow;

            try
            {
                await task.ExecuteAsync(context, ct);
            }
            catch (Exception ex)
            {
                if (context.Outcome is not JobOutcome.Failure)
                    context.ReportFailure(ex.Message);
                _logger.LogError(ex, "Job '{JobName}' threw an unhandled exception", trigger.JobName);
            }

            var completedAt = DateTime.UtcNow;
            var finalOutcome = context.Outcome;
            var succeeded = finalOutcome is JobOutcome.Success;
            var message = finalOutcome switch
            {
                JobOutcome.Success s => s.Message,
                JobOutcome.Failure f => f.Error,
                _ => "Task did not report an outcome."
            };

            if (finalOutcome == null || finalOutcome is JobOutcome.Progress)
                succeeded = false;

            await PersistResultAsync(scope, trigger, executionId, startedAt, completedAt, succeeded, message, ct);

            _monitor.Record(new JobExecutionRecord
            {
                JobName = trigger.JobName,
                ExecutionId = executionId,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Succeeded = succeeded,
                Message = message,
                Source = trigger.Source
            });
        }

        private async Task PersistResultAsync(
            IServiceScope scope,
            JobTriggerRequest trigger,
            string executionId,
            DateTime startedAt,
            DateTime completedAt,
            bool succeeded,
            string? message,
            CancellationToken ct)
        {
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TimeManagementDbContext>();
                var config = _configs.FirstOrDefault(c => c.JobName == trigger.JobName);

                var scheduledJob = await dbContext.ScheduledJobs
                    .FirstOrDefaultAsync(j => j.Name.Value == trigger.JobName, ct);

                if (scheduledJob == null && config != null)
                {
                    scheduledJob = ScheduledJob.Create(
                        trigger.JobName, config.JobType, config.CronExpression, config.TimeZoneId, config.MaxRetries);
                    dbContext.ScheduledJobs.Add(scheduledJob);
                    await dbContext.SaveChangesAsync(ct);
                }
                else if (scheduledJob != null && config != null)
                {
                    if (scheduledJob.SyncConfig(config.CronExpression, config.TimeZoneId, config.MaxRetries))
                        await dbContext.SaveChangesAsync(ct);
                }

                if (scheduledJob == null)
                {
                    _logger.LogWarning("Cannot persist result for unknown job '{JobName}'", trigger.JobName);
                    return;
                }

                DateTime? nextRunAt = null;
                if (config?.JobType == JobType.Recurring && config.CronExpression != null)
                {
                    var cron = CronExpression.Parse(config.CronExpression);
                    var tz = string.IsNullOrEmpty(config.TimeZoneId)
                        ? TimeZoneInfo.Utc
                        : TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
                    nextRunAt = cron.GetNextOccurrence(completedAt, tz);
                }

                scheduledJob.RecordRun(completedAt, nextRunAt);

                DeferredJobInstanceId? deferredInstanceId = trigger.DeferredInstanceId.HasValue
                    ? new DeferredJobInstanceId(trigger.DeferredInstanceId.Value)
                    : null;

                var execution = JobExecution.Record(
                    scheduledJob.Id,
                    deferredInstanceId,
                    (byte)trigger.Source,
                    executionId,
                    startedAt,
                    completedAt,
                    succeeded,
                    message);

                dbContext.JobExecutions.Add(execution);

                if (trigger.DeferredInstanceId.HasValue)
                {
                    var instanceId = new DeferredJobInstanceId(trigger.DeferredInstanceId.Value);
                    var instance = await dbContext.DeferredJobInstances
                        .FirstOrDefaultAsync(d => d.Id == instanceId, ct);
                    if (instance != null)
                    {
                        if (succeeded)
                            instance.Complete();
                        else
                            instance.Fail(message ?? "Unknown error");
                    }
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist execution result for job '{JobName}'", trigger.JobName);
            }
        }
    }
}
