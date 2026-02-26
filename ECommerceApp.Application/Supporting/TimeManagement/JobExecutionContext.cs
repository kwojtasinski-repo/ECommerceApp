using ECommerceApp.Application.Supporting.TimeManagement.Models;
using System;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public sealed class JobExecutionContext
    {
        private JobOutcome? _outcome;

        public string? EntityId { get; }
        public string ExecutionId { get; }
        public DateTime StartedAt { get; }
        public JobOutcome? Outcome => _outcome;

        public JobExecutionContext(string? entityId, string executionId)
        {
            EntityId = entityId;
            ExecutionId = executionId;
            StartedAt = DateTime.UtcNow;
        }

        public void ReportSuccess(string? message = null)
            => _outcome = JobOutcome.Succeeded(message);

        public void ReportFailure(string error)
            => _outcome = JobOutcome.Failed(error);

        public void ReportProgress(string message)
            => _outcome = JobOutcome.InProgress(message);
    }
}
