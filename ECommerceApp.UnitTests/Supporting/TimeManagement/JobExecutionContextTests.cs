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
            var runAt = DateTime.UtcNow.AddMinutes(15);

            var instance = DeferredJobInstance.Schedule("PaymentTimeout", "42", runAt);

            instance.JobName.Should().Be("PaymentTimeout");
            instance.EntityId.Should().Be("42");
            instance.RunAt.Should().Be(runAt);
            instance.Status.Should().Be(DeferredJobStatus.Pending);
            instance.RetryCount.Should().Be(0);
        }

        [Fact]
        public void DeferredJobInstance_Fail_IncrementsRetryCountAndMovesToPendingOrDeadLetter()
        {
            var instance = DeferredJobInstance.Schedule("PaymentTimeout", "1", DateTime.UtcNow.AddMinutes(15));
            var failedAt = DateTime.UtcNow;

            instance.Fail("error", failedAt);

            instance.RetryCount.Should().Be(1);
            instance.ErrorMessage.Should().Be("error");
            // RetryCount(1) <= MaxRetries(3): back to Pending with a new RunAt
            instance.Status.Should().Be(DeferredJobStatus.Pending);
            instance.RunAt.Should().BeAfter(failedAt);
        }

        [Fact]
        public void DeferredJobInstance_MarkRunning_SetsStatusAndLockExpiresAt()
        {
            var instance = DeferredJobInstance.Schedule("PaymentTimeout", "1", DateTime.UtcNow.AddMinutes(15));
            var lockExpiry = DateTime.UtcNow.AddMinutes(5);

            instance.MarkRunning(lockExpiry);

            instance.Status.Should().Be(DeferredJobStatus.Running);
            instance.LockExpiresAt.Should().Be(lockExpiry);
        }

        [Fact]
        public void DeferredJobInstance_Fail_ExhaustedRetries_MovesToDeadLetter()
        {
            var instance = DeferredJobInstance.Schedule("PaymentTimeout", "1", DateTime.UtcNow.AddMinutes(15), maxRetries: 1);
            var failedAt = DateTime.UtcNow;
            instance.Fail("err1", failedAt);

            instance.Fail("err2", failedAt);

            instance.Status.Should().Be(DeferredJobStatus.DeadLetter);
        }
    }
}
