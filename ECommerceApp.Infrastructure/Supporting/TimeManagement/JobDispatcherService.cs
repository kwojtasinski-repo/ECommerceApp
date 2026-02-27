using Cronos;
using ECommerceApp.Application.Supporting.TimeManagement;
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
    internal sealed class JobDispatcherService : BackgroundService
    {
        private readonly JobTriggerChannel _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly InMemoryJobStatusMonitor _monitor;
        private readonly ILogger<JobDispatcherService> _logger;

        public JobDispatcherService(
            JobTriggerChannel channel,
            IServiceScopeFactory scopeFactory,
            InMemoryJobStatusMonitor monitor,
            ILogger<JobDispatcherService> logger)
        {
            _channel = channel;
            _scopeFactory = scopeFactory;
            _monitor = monitor;
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

                // For recurring / manual jobs: update LastRunAt / NextRunAt on ScheduledJob
                if (trigger.Source != JobTriggerSource.Deferred)
                {
                    var scheduledJob = await dbContext.ScheduledJobs
                        .FirstOrDefaultAsync(j => j.Name.Value == trigger.JobName, ct);

                    if (scheduledJob != null)
                    {
                        DateTime? nextRunAt = null;
                        try
                        {
                            var cron = CronExpression.Parse(scheduledJob.Schedule);
                            var tz = string.IsNullOrEmpty(scheduledJob.TimeZoneId)
                                ? TimeZoneInfo.Utc
                                : TimeZoneInfo.FindSystemTimeZoneById(scheduledJob.TimeZoneId);
                            nextRunAt = cron.GetNextOccurrence(completedAt, tz);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not parse schedule for job '{JobName}'", trigger.JobName);
                        }

                        scheduledJob.RecordRun(completedAt, nextRunAt);
                    }
                }

                // Append-only audit record (A2: JobName string, no FK)
                var execution = JobExecution.Record(
                    trigger.JobName,
                    trigger.DeferredInstanceId,
                    (byte)trigger.Source,
                    executionId,
                    startedAt,
                    completedAt,
                    succeeded,
                    message);
                dbContext.JobExecutions.Add(execution);

                // For deferred jobs: DELETE on success, Fail() on failure (handles retry / DeadLetter)
                if (trigger.DeferredInstanceId.HasValue)
                {
                    var instance = await dbContext.DeferredJobQueue
                        .FirstOrDefaultAsync(d => d.Id.Value == trigger.DeferredInstanceId.Value, ct);

                    if (instance != null)
                    {
                        if (succeeded)
                            dbContext.DeferredJobQueue.Remove(instance);
                        else
                            instance.Fail(message ?? "Unknown error", completedAt);
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
