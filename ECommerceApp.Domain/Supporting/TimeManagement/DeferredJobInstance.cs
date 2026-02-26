using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class DeferredJobInstance
    {
        public DeferredJobInstanceId Id { get; private set; } = new DeferredJobInstanceId(0);
        public ScheduledJobId ScheduledJobId { get; private set; } = default!;
        public ScheduledJob? ScheduledJob { get; private set; }
        public string EntityId { get; private set; } = default!;
        public DateTime RunAt { get; private set; }
        public DeferredJobStatus Status { get; private set; }
        public int RetryCount { get; private set; }
        public string? ErrorMessage { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private DeferredJobInstance() { }

        public static DeferredJobInstance Schedule(ScheduledJobId scheduledJobId, string entityId, DateTime runAt)
        {
            return new DeferredJobInstance
            {
                ScheduledJobId = scheduledJobId,
                EntityId = entityId,
                RunAt = runAt,
                Status = DeferredJobStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void MarkRunning()
        {
            Status = DeferredJobStatus.Running;
        }

        public void Complete()
        {
            Status = DeferredJobStatus.Completed;
        }

        public void Fail(string errorMessage)
        {
            Status = DeferredJobStatus.Failed;
            ErrorMessage = errorMessage;
            RetryCount++;
        }

        public void Cancel()
        {
            Status = DeferredJobStatus.Cancelled;
        }
    }
}
