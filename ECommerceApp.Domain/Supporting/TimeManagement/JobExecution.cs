using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class JobExecution
    {
        public JobExecutionId Id { get; private set; } = new JobExecutionId(0);
        public string JobName { get; private set; } = default!;
        public int? DeferredQueueId { get; private set; }
        public byte Source { get; private set; }
        public string ExecutionId { get; private set; } = default!;
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public bool Succeeded { get; private set; }
        public string? Message { get; private set; }

        private JobExecution() { }

        public static JobExecution Record(
            string jobName,
            int? deferredQueueId,
            byte source,
            string executionId,
            DateTime startedAt,
            DateTime completedAt,
            bool succeeded,
            string? message)
        {
            return new JobExecution
            {
                JobName = jobName,
                DeferredQueueId = deferredQueueId,
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
