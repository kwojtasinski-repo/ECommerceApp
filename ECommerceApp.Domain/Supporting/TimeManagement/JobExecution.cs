using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class JobExecution
    {
        public JobExecutionId Id { get; private set; } = default!;
        public JobName JobName { get; private set; } = default!;
        public DeferredJobInstanceId? DeferredQueueId { get; private set; }
        public JobTriggerSource Source { get; private set; }
        public string ExecutionId { get; private set; } = default!;
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public bool Succeeded { get; private set; }
        public string? Message { get; private set; }

        private JobExecution() { }

        public static JobExecution Record(
            string jobName,
            int? deferredQueueId,
            JobTriggerSource source,
            string executionId,
            DateTime startedAt,
            DateTime completedAt,
            bool succeeded,
            string? message)
        {
            return new JobExecution
            {
                JobName = new JobName(jobName),
                DeferredQueueId = deferredQueueId.HasValue ? new DeferredJobInstanceId(deferredQueueId.Value) : null,
                Source = source,
                ExecutionId = executionId,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Succeeded = succeeded,
                Message = message
            };
        }
    }
}
