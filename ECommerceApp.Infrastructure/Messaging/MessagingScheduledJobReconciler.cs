using Cronos;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class MessagingScheduledJobReconciler : IHostedService
    {
        private const string OutboxJobName = "OutboxCleanup";
        private const string InboxJobName = "InboxCleanup";
        private const string DefaultOutboxSchedule = "0 3 * * *";
        private const string DefaultInboxSchedule = "30 3 * * *";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MessagingOptions _options;
        private readonly ILogger<MessagingScheduledJobReconciler> _logger;

        public MessagingScheduledJobReconciler(
            IServiceScopeFactory scopeFactory,
            MessagingOptions options,
            ILogger<MessagingScheduledJobReconciler> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ReconcileAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconcile messaging cleanup jobs.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        internal async Task ReconcileAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();

            await ReconcileJobAsync(
                repository,
                OutboxJobName,
                _options.OutboxCleanupSchedule,
                DefaultOutboxSchedule,
                cancellationToken);
            await ReconcileJobAsync(
                repository,
                InboxJobName,
                _options.InboxCleanupSchedule,
                DefaultInboxSchedule,
                cancellationToken);
        }

        private async Task ReconcileJobAsync(
            IScheduledJobRepository repository,
            string jobName,
            string configuredSchedule,
            string defaultSchedule,
            CancellationToken cancellationToken)
        {
            // Configuration is authoritative for these two jobs; admin changes are reconciled on restart.
            var job = await repository.GetByNameAsync(jobName, cancellationToken);
            var schedule = ResolveSchedule(jobName, configuredSchedule, job?.Schedule.Value, defaultSchedule);

            if (job is null)
            {
                job = ScheduledJob.Create(jobName, schedule, null, 3);
                SetEnabled(job);
                await repository.AddAsync(job, cancellationToken);
                return;
            }

            var changed = false;
            if (job.Schedule.Value != schedule)
            {
                job.UpdateSchedule(schedule);
                changed = true;
            }

            if (job.IsEnabled != _options.CleanupEnabled)
            {
                SetEnabled(job);
                changed = true;
            }

            if (changed)
            {
                await repository.UpdateAsync(job, cancellationToken);
            }
        }

        private string ResolveSchedule(string jobName, string configured, string previous, string fallback)
        {
            try
            {
                CronExpression.Parse(configured);
                return configured;
            }
            catch (Exception ex)
            {
                var safeFallback = previous ?? fallback;
                _logger.LogWarning(ex,
                    "Invalid cron schedule for messaging job '{JobName}'. Using '{Schedule}'.",
                    jobName,
                    safeFallback);
                return safeFallback;
            }
        }

        private void SetEnabled(ScheduledJob job)
        {
            if (_options.CleanupEnabled)
                job.Enable();
            else
                job.Disable();
        }
    }
}