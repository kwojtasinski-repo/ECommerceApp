using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging.Services
{
    internal sealed class OutboxCleanupTask : IScheduledTask
    {
        private readonly IOutboxRepository _outbox;
        private readonly MessagingOptions _options;

        public OutboxCleanupTask(IOutboxRepository outbox, MessagingOptions options)
        {
            _outbox = outbox;
            _options = options;
        }

        public string TaskName => "OutboxCleanup";

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (!_options.CleanupEnabled)
            {
                context.ReportSuccess("Skipped: cleanup disabled via MessagingOptions.CleanupEnabled");
                return;
            }

            try
            {
                var cutoff = DateTime.UtcNow - _options.OutboxRetention;
                var deleted = await _outbox.DeleteDispatchedOlderThanAsync(cutoff, cancellationToken);
                context.ReportSuccess($"Deleted {deleted} dispatched outbox row(s) older than {_options.OutboxRetention}.");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}