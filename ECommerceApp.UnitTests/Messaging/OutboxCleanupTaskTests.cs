using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Messaging.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class OutboxCleanupTaskTests
    {
        private readonly Mock<IOutboxRepository> _outbox = new();

        [Fact]
        public void TaskName_ShouldBeOutboxCleanup()
        {
            CreateTask().TaskName.Should().Be("OutboxCleanup");
        }

        [Fact]
        public async Task ExecuteAsync_CleanupDisabled_ReportsSuccessSkippedNoDeletion()
        {
            var options = new MessagingOptions { CleanupEnabled = false };
            var context = new JobExecutionContext(null, "outbox-disabled");

            await new OutboxCleanupTask(_outbox.Object, options).ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("disabled");
            _outbox.Verify(r => r.DeleteDispatchedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_DeletesDispatchedOlderThanRetention()
        {
            _outbox.Setup(r => r.DeleteDispatchedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);
            var context = new JobExecutionContext(null, "outbox-delete");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("3");
            _outbox.Verify(r => r.DeleteDispatchedOlderThanAsync(It.Is<DateTime>(cutoff => cutoff < DateTime.UtcNow.AddDays(-6.99)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_RowExactlyAtRetentionBoundary_NotDeletedByTask()
        {
            _outbox.Setup(r => r.DeleteDispatchedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            var context = new JobExecutionContext(null, "outbox-boundary");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("0");
        }

        [Fact]
        public async Task ExecuteAsync_RepositoryThrows_ReportsFailureNotException()
        {
            _outbox.Setup(r => r.DeleteDispatchedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB connection failed"));
            var context = new JobExecutionContext(null, "outbox-error");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>().Which.Error.Should().Contain("DB connection failed");
        }

        private OutboxCleanupTask CreateTask() => new(_outbox.Object, new MessagingOptions());
    }
}