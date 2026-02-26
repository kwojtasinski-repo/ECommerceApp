using ECommerceApp.Domain.Supporting.TimeManagement;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.TimeManagement
{
    public class ScheduledJobAggregateTests
    {
        [Fact]
        public void Create_ValidParameters_ShouldCreateEnabledJob()
        {
            var job = ScheduledJob.Create("CurrencyRateSync", JobType.Recurring, "15 12 * * *", null, 3);

            job.Name.Value.Should().Be("CurrencyRateSync");
            job.JobType.Should().Be(JobType.Recurring);
            job.CronExpression.Should().Be("15 12 * * *");
            job.IsEnabled.Should().BeTrue();
            job.MaxRetries.Should().Be(3);
            job.LastRunAt.Should().BeNull();
            job.NextRunAt.Should().BeNull();
            job.ConfigHash.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Enable_SetsIsEnabledTrue()
        {
            var job = ScheduledJob.Create("TestJob", JobType.Recurring, "0 * * * *", null, 1);
            job.Disable();

            job.Enable();

            job.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void Disable_SetsIsEnabledFalse()
        {
            var job = ScheduledJob.Create("TestJob", JobType.Recurring, "0 * * * *", null, 1);

            job.Disable();

            job.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void RecordRun_UpdatesLastRunAtAndNextRunAt()
        {
            var job = ScheduledJob.Create("TestJob", JobType.Recurring, "0 * * * *", null, 1);
            var completedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var nextRunAt = completedAt.AddHours(1);

            job.RecordRun(completedAt, nextRunAt);

            job.LastRunAt.Should().Be(completedAt);
            job.NextRunAt.Should().Be(nextRunAt);
        }

        [Fact]
        public void SyncConfig_DifferentValues_ReturnsTrueAndUpdates()
        {
            var job = ScheduledJob.Create("TestJob", JobType.Recurring, "0 * * * *", null, 3);

            var changed = job.SyncConfig("15 12 * * *", "UTC", 5);

            changed.Should().BeTrue();
            job.CronExpression.Should().Be("15 12 * * *");
            job.TimeZoneId.Should().Be("UTC");
            job.MaxRetries.Should().Be(5);
        }

        [Fact]
        public void SyncConfig_SameValues_ReturnsFalse()
        {
            var job = ScheduledJob.Create("TestJob", JobType.Recurring, "0 * * * *", null, 3);

            var changed = job.SyncConfig("0 * * * *", null, 3);

            changed.Should().BeFalse();
        }

        [Fact]
        public void Create_DeferredType_NoCronExpression()
        {
            var job = ScheduledJob.Create("PaymentTimeout", JobType.Deferred, null, null, 2);

            job.JobType.Should().Be(JobType.Deferred);
            job.CronExpression.Should().BeNull();
            job.MaxRetries.Should().Be(2);
        }
    }
}
