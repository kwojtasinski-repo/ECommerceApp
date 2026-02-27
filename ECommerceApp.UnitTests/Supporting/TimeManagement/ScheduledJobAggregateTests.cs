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
            var job = ScheduledJob.Create("CurrencyRateSync", "15 12 * * *", null, 3);

            job.Name.Value.Should().Be("CurrencyRateSync");
            job.Schedule.Should().Be("15 12 * * *");
            job.IsEnabled.Should().BeTrue();
            job.MaxRetries.Should().Be(3);
            job.LastRunAt.Should().BeNull();
            job.NextRunAt.Should().BeNull();
        }

        [Fact]
        public void Enable_SetsIsEnabledTrue()
        {
            var job = ScheduledJob.Create("TestJob", "0 * * * *", null, 1);
            job.Disable();

            job.Enable();

            job.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void Disable_SetsIsEnabledFalse()
        {
            var job = ScheduledJob.Create("TestJob", "0 * * * *", null, 1);

            job.Disable();

            job.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void RecordRun_UpdatesLastRunAtAndNextRunAt()
        {
            var job = ScheduledJob.Create("TestJob", "0 * * * *", null, 1);
            var completedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var nextRunAt = completedAt.AddHours(1);

            job.RecordRun(completedAt, nextRunAt);

            job.LastRunAt.Should().Be(completedAt);
            job.NextRunAt.Should().Be(nextRunAt);
        }
    }
}
