using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    /// <summary>Removes abandoned upload sessions (memory + temp files) after <see cref="ChunkedUploadOptions.SessionTtl"/>.</summary>
    internal sealed class ChunkedUploadCleanupTask : IScheduledTask
    {
        public const string JobName = "ChunkedUploadCleanup";

        public string TaskName => JobName;

        private readonly UploadSessionStore _store;
        private readonly IOptions<ChunkedUploadOptions> _options;

        public ChunkedUploadCleanupTask(UploadSessionStore store, IOptions<ChunkedUploadOptions> options)
        {
            _store = store;
            _options = options;
        }

        public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var options = _options.Value;
                if (!options.Enabled)
                {
                    context.ReportSuccess("Disabled via configuration; no upload sessions evaluated.");
                    return Task.CompletedTask;
                }

                var removed = _store.RemoveExpired(DateTime.UtcNow - options.SessionTtl);
                context.ReportSuccess($"Removed {removed} abandoned upload session(s) inactive for over {options.SessionTtl}.");
            }
            catch (Exception ex)
            {
                context.ReportFailure(ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
