using Cronos;
using ECommerceApp.Application.Catalog.Images.ChunkedUpload;
using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class ChunkedUploadCleanupScheduledJobReconciler : IHostedService
    {
        private const string JobName = "ChunkedUploadCleanup";
        private const string DefaultSchedule = "0 * * * *";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<ChunkedUploadOptions> _options;
        private readonly ILogger<ChunkedUploadCleanupScheduledJobReconciler> _logger;

        public ChunkedUploadCleanupScheduledJobReconciler(
            IServiceScopeFactory scopeFactory,
            IOptions<ChunkedUploadOptions> options,
            ILogger<ChunkedUploadCleanupScheduledJobReconciler> logger)
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
                _logger.LogError(ex, "Failed to reconcile the chunked-upload cleanup job.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        internal async Task ReconcileAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
            var options = _options.Value;

            var job = await repository.GetByNameAsync(JobName, cancellationToken);
            var schedule = ResolveSchedule(options.Schedule, job?.Schedule.Value);

            if (job is null)
            {
                job = ScheduledJob.Create(JobName, schedule, null, 3);
                SetEnabled(job, options.Enabled);
                await repository.AddAsync(job, cancellationToken);
                return;
            }

            var changed = false;
            if (job.Schedule.Value != schedule)
            {
                job.UpdateSchedule(schedule);
                changed = true;
            }

            if (job.IsEnabled != options.Enabled)
            {
                SetEnabled(job, options.Enabled);
                changed = true;
            }

            if (changed)
            {
                await repository.UpdateAsync(job, cancellationToken);
            }
        }

        private string ResolveSchedule(string configured, string previous)
        {
            try
            {
                CronExpression.Parse(configured);
                return configured;
            }
            catch (Exception ex)
            {
                var safeFallback = previous ?? DefaultSchedule;
                _logger.LogWarning(ex,
                    "Invalid cron schedule for job '{JobName}'. Using '{Schedule}'.",
                    JobName,
                    safeFallback);
                return safeFallback;
            }
        }

        private static void SetEnabled(ScheduledJob job, bool enabled)
        {
            if (enabled)
                job.Enable();
            else
                job.Disable();
        }
    }
}
