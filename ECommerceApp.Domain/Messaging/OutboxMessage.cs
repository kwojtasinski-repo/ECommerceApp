using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Messaging
{
    public class OutboxMessage
    {
        private static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromHours(1);

        public long Id { get; private set; }
        public string MessageTypeKey { get; private set; } = default!;
        public string Payload { get; private set; } = default!;
        public DateTime CreatedAt { get; private set; }
        public OutboxStatus Status { get; private set; }
        public DateTime? DispatchedAt { get; private set; }
        public DateTime? LockExpiresAt { get; private set; }
        public DateTime NextAttemptAt { get; private set; }

        public int RetryCount { get; private set; }
        public int MaxRetries { get; private set; }
        public string ErrorMessage { get; private set; }

        private OutboxMessage() { }

        public static OutboxMessage Create(string messageTypeKey, string payload, int maxRetries = 5)
        {
            if (string.IsNullOrWhiteSpace(messageTypeKey))
                throw new DomainException("Message type key must not be empty.");
            if (payload is null)
                throw new DomainException("Payload must not be null.");
            if (maxRetries < 0)
                throw new DomainException("Max retries must be non-negative.");

            var now = DateTime.UtcNow;
            return new OutboxMessage
            {
                MessageTypeKey = messageTypeKey,
                Payload = payload,
                CreatedAt = now,
                Status = OutboxStatus.Pending,
                NextAttemptAt = now,
                RetryCount = 0,
                MaxRetries = maxRetries
            };
        }

        public void MarkRunning(DateTime lockExpiresAt)
        {
            Status = OutboxStatus.Running;
            LockExpiresAt = lockExpiresAt;
        }

        public void MarkDispatched(DateTime dispatchedAt)
        {
            Status = OutboxStatus.Dispatched;
            DispatchedAt = dispatchedAt;
            LockExpiresAt = null;
        }

        public void Fail(string errorMessage, DateTime failedAt, TimeSpan? maxBackoff = null)
        {
            ErrorMessage = errorMessage;
            LockExpiresAt = null;
            RetryCount++;
            if (RetryCount <= MaxRetries)
            {
                NextAttemptAt = ComputeRetryRunAt(failedAt, maxBackoff ?? DefaultMaxBackoff);
                Status = OutboxStatus.Pending;
            }
            else
            {
                Status = OutboxStatus.DeadLetter;
            }
        }

        public void ResetZombie(DateTime detectedAt, TimeSpan? maxBackoff = null)
        {
            LockExpiresAt = null;
            RetryCount++;
            if (RetryCount <= MaxRetries)
            {
                NextAttemptAt = ComputeRetryRunAt(detectedAt, maxBackoff ?? DefaultMaxBackoff);
                Status = OutboxStatus.Pending;
            }
            else
            {
                Status = OutboxStatus.DeadLetter;
            }
        }

        private DateTime ComputeRetryRunAt(DateTime failedAt, TimeSpan maxBackoff)
        {
            var originalDelay = NextAttemptAt > CreatedAt ? NextAttemptAt - CreatedAt : TimeSpan.FromMinutes(1);
            var factor = originalDelay.TotalMinutes * 0.1 * Math.Max(RetryCount, 1);
            var backoff = TimeSpan.FromMinutes(Math.Max(factor, 1.0));
            if (backoff > maxBackoff)
                backoff = maxBackoff;
            return failedAt + backoff;
        }
    }
}
