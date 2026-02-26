using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.TimeManagement
{
    public class JobExecutionContextTests
    {
        [Fact]
        public void InitialOutcome_IsNull()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            context.Outcome.Should().BeNull();
        }

        [Fact]
        public void ReportSuccess_SetsSuccessOutcome()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            context.ReportSuccess("done");

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            ((JobOutcome.Success)context.Outcome!).Message.Should().Be("done");
        }

        [Fact]
        public void ReportSuccess_NoMessage_SetsNullMessage()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            context.ReportSuccess();

            var success = context.Outcome.Should().BeOfType<JobOutcome.Success>().Subject;
            success.Message.Should().BeNull();
        }

        [Fact]
        public void ReportFailure_SetsFailureOutcome()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            context.ReportFailure("something went wrong");

            var failure = context.Outcome.Should().BeOfType<JobOutcome.Failure>().Subject;
            failure.Error.Should().Be("something went wrong");
        }

        [Fact]
        public void ReportProgress_SetsProgressOutcome()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            context.ReportProgress("processing...");

            var progress = context.Outcome.Should().BeOfType<JobOutcome.Progress>().Subject;
            progress.Message.Should().Be("processing...");
        }

        [Fact]
        public void ReportSuccess_AfterProgress_OverwritesOutcome()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());
            context.ReportProgress("step 1");

            context.ReportSuccess("all done");

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
        }

        [Fact]
        public void EntityId_IsPassedThrough()
        {
            var context = new JobExecutionContext("42", "exec-id");

            context.EntityId.Should().Be("42");
            context.ExecutionId.Should().Be("exec-id");
        }

        [Fact]
        public void StartedAt_IsSetOnConstruction()
        {
            var before = DateTime.UtcNow;
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());
            var after = DateTime.UtcNow;

            context.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        [Fact]
        public void DeferredJobInstance_Schedule_SetsCorrectFields()
        {
            var scheduledJobId = new ScheduledJobId(1);
            var runAt = DateTime.UtcNow.AddMinutes(15);

            var instance = DeferredJobInstance.Schedule(scheduledJobId, "42", runAt);

            instance.ScheduledJobId.Should().Be(scheduledJobId);
            instance.EntityId.Should().Be("42");
            instance.RunAt.Should().Be(runAt);
            instance.Status.Should().Be(DeferredJobStatus.Pending);
            instance.RetryCount.Should().Be(0);
        }

        [Fact]
        public void DeferredJobInstance_Fail_IncrementsRetryCount()
        {
            var instance = DeferredJobInstance.Schedule(new ScheduledJobId(1), "1", DateTime.UtcNow);

            instance.Fail("error");

            instance.Status.Should().Be(DeferredJobStatus.Failed);
            instance.RetryCount.Should().Be(1);
            instance.ErrorMessage.Should().Be("error");
        }

        [Fact]
        public void DeferredJobInstance_Complete_SetsStatusCompleted()
        {
            var instance = DeferredJobInstance.Schedule(new ScheduledJobId(1), "1", DateTime.UtcNow);
            instance.MarkRunning();

            instance.Complete();

            instance.Status.Should().Be(DeferredJobStatus.Completed);
        }

        [Fact]
        public void DeferredJobInstance_Cancel_SetsStatusCancelled()
        {
            var instance = DeferredJobInstance.Schedule(new ScheduledJobId(1), "1", DateTime.UtcNow);

            instance.Cancel();

            instance.Status.Should().Be(DeferredJobStatus.Cancelled);
        }
    }
}
