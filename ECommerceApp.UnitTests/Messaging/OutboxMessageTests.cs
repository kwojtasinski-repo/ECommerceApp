using AwesomeAssertions;
using ECommerceApp.Domain.Messaging;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class OutboxMessageTests
    {
        private static DateTime FailMessageRepeatedly(OutboxMessage message, DateTime failedAt, int failureCount)
        {
            for (var attempt = 0; attempt < failureCount; attempt++)
            {
                failedAt = failedAt.AddMinutes(1);
                message.Fail("boom", failedAt);
            }

            return failedAt;
        }

        [Fact]
        public void Create_SetsStatusPending()
        {
            var message = OutboxMessage.Create("test-message", "{}", 5);

            message.Status.Should().Be(OutboxStatus.Pending);
            message.MessageTypeKey.Should().Be("test-message");
            message.Payload.Should().Be("{}");
            message.MaxRetries.Should().Be(5);
            message.RetryCount.Should().Be(0);
            message.DispatchedAt.Should().BeNull();
            message.LockExpiresAt.Should().BeNull();
            message.ErrorMessage.Should().BeNull();
            message.NextAttemptAt.Should().Be(message.CreatedAt);
        }

        [Fact]
        public void MarkDispatched_SetsStatusAndTimestamp()
        {
            var message = OutboxMessage.Create("test-message", "{}");
            var dispatchedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            message.MarkDispatched(dispatchedAt);

            message.Status.Should().Be(OutboxStatus.Dispatched);
            message.DispatchedAt.Should().Be(dispatchedAt);
            message.LockExpiresAt.Should().BeNull();
        }

        [Fact]
        public void Fail_BelowMaxRetries_ReturnsToPendingWithBackoff()
        {
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 3);
            var failedAt = message.CreatedAt.AddMinutes(1);

            message.Fail("boom", failedAt);

            message.Status.Should().Be(OutboxStatus.Pending);
            message.RetryCount.Should().Be(1);
            message.ErrorMessage.Should().Be("boom");
            message.LockExpiresAt.Should().BeNull();
            message.NextAttemptAt.Should().BeAfter(failedAt.AddSeconds(-1));
        }

        [Fact]
        public void Fail_ExceedsMaxRetries_TransitionsToDeadLetter()
        {
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 1);
            var failedAt = message.CreatedAt.AddMinutes(1);

            message.Fail("boom", failedAt);
            message.Fail("boom again", failedAt.AddMinutes(1));

            message.Status.Should().Be(OutboxStatus.DeadLetter);
            message.RetryCount.Should().Be(2);
        }

        [Fact]
        public void ResetZombie_ClearsLockAndIncrementsRetry()
        {
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 3);
            message.MarkRunning(message.CreatedAt.AddMinutes(5));
            var detectedAt = message.CreatedAt.AddMinutes(10);

            message.ResetZombie(detectedAt);

            message.Status.Should().Be(OutboxStatus.Pending);
            message.RetryCount.Should().Be(1);
            message.LockExpiresAt.Should().BeNull();
            message.NextAttemptAt.Should().BeAfter(detectedAt.AddSeconds(-1));
        }

        [Fact]
        public void Create_NegativeMaxRetries_Throws()
        {
            Action act = () => OutboxMessage.Create("test-message", "{}", -1);

            act.Should().Throw<ECommerceApp.Domain.Shared.DomainException>();
        }

        [Fact]
        public void Create_EmptyMessageTypeKey_Throws()
        {
            Action act = () => OutboxMessage.Create("", "{}");

            act.Should().Throw<ECommerceApp.Domain.Shared.DomainException>();
        }

        // ── Configurable backoff cap (RetryPolicyOptions.MaxBackoff at the call site) ──────────

        [Fact]
        public void Fail_WithCustomMaxBackoff_ClampsToProvidedCap()
        {
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 3);
            var failedAt = message.CreatedAt.AddMinutes(1);
            var cap = TimeSpan.FromSeconds(10);

            message.Fail("boom", failedAt, cap);

            message.NextAttemptAt.Should().Be(failedAt + cap);
        }

        [Fact]
        public void ResetZombie_WithCustomMaxBackoff_ClampsToProvidedCap()
        {
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 3);
            message.MarkRunning(message.CreatedAt.AddMinutes(5));
            var detectedAt = message.CreatedAt.AddMinutes(10);
            var cap = TimeSpan.FromSeconds(5);

            message.ResetZombie(detectedAt, cap);

            message.NextAttemptAt.Should().Be(detectedAt + cap);
        }

        [Fact]
        public void Fail_WithoutExplicitMaxBackoff_DefaultsToOneHourCap()
        {
            // Drive RetryCount up so the computed backoff would exceed one hour if uncapped,
            // proving the 1-hour DefaultMaxBackoff fallback is actually applied when the caller
            // (as today's tests do) omits the maxBackoff argument.
            var message = OutboxMessage.Create("test-message", "{}", maxRetries: 50);
            var failedAt = FailMessageRepeatedly(message, message.CreatedAt, failureCount: 20);

            (message.NextAttemptAt - failedAt).Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1));
        }
    }
}
