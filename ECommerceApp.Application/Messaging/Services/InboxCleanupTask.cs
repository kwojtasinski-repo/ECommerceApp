using ECommerceApp.Application.Supporting.TimeManagement;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging.Services
{
    internal sealed class InboxCleanupTask : IScheduledTask
    {
        private readonly IInboxCleanupRepository _inbox;
        private readonly MessagingOptions _options;

        public InboxCleanupTask(IInboxCleanupRepository inbox, MessagingOptions options)
        {
            _inbox = inbox;
            _options = options;
        }

        public string TaskName => "InboxCleanup";

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (!_options.CleanupEnabled)
            {
                context.ReportSuccess("Skipped: cleanup disabled via MessagingOptions.CleanupEnabled");
                return;
            }

            try
            {
                var cutoff = DateTime.UtcNow - _options.InboxRetention;
                var deleted = await _inbox.DeleteProcessedOlderThanAsync(cutoff, cancellationToken);
                context.ReportSuccess($"Deleted {deleted} processed inbox row(s) older than {_options.InboxRetention}.");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }
        }
    }
}