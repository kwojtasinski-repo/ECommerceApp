using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class DeferredJobInstance
    {
        public DeferredJobInstanceId Id { get; private set; } = default!;
        public JobName JobName { get; private set; } = default!;
        public EntityId EntityId { get; private set; } = default!;
        public DateTime RunAt { get; private set; }
        public DeferredJobStatus Status { get; private set; }
        public int RetryCount { get; private set; }
        public int MaxRetries { get; private set; }
        public DateTime? LockExpiresAt { get; private set; }
        public string? ErrorMessage { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private DeferredJobInstance() { }

        public static DeferredJobInstance Schedule(string jobName, string entityId, DateTime runAt, int maxRetries = 3)
        {
            if (maxRetries < 0)
                throw new DomainException("Max retries must be non-negative.");
            var runAtMinute = new DateTime(runAt.Year, runAt.Month, runAt.Day, runAt.Hour, runAt.Minute, 0, DateTimeKind.Utc);
            return new DeferredJobInstance
            {
                JobName = new JobName(jobName),
                EntityId = new EntityId(entityId),
                RunAt = runAtMinute,
                Status = DeferredJobStatus.Pending,
                RetryCount = 0,
                MaxRetries = maxRetries,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void MarkRunning(DateTime lockExpiresAt)
        {
            Status = DeferredJobStatus.Running;
            LockExpiresAt = lockExpiresAt;
        }

        public void Fail(string errorMessage, DateTime failedAt)
        {
            ErrorMessage = errorMessage;
            LockExpiresAt = null;
            RetryCount++;
            if (RetryCount <= MaxRetries)
            {
                RunAt = ComputeRetryRunAt(failedAt, TimeSpan.FromHours(1));
                Status = DeferredJobStatus.Pending;
            }
            else
            {
                Status = DeferredJobStatus.DeadLetter;
            }
        }

        public void ResetZombie(DateTime detectedAt)
        {
            LockExpiresAt = null;
            RetryCount++;
            if (RetryCount <= MaxRetries)
            {
                RunAt = ComputeRetryRunAt(detectedAt, TimeSpan.FromHours(1));
                Status = DeferredJobStatus.Pending;
            }
            else
            {
                Status = DeferredJobStatus.DeadLetter;
            }
        }

        public DateTime ComputeRetryRunAt(DateTime failedAt, TimeSpan maxCap)
        {
            var originalDelay = RunAt > CreatedAt ? RunAt - CreatedAt : TimeSpan.FromMinutes(1);
            var factor = originalDelay.TotalMinutes * 0.1 * Math.Max(RetryCount, 1);
            var backoff = TimeSpan.FromMinutes(Math.Max(factor, 1.0));
            if (backoff > maxCap)
                backoff = maxCap;
            return failedAt + backoff;
        }
    }
}
