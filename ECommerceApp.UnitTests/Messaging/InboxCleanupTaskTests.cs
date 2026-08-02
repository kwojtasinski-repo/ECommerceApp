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
    public class InboxCleanupTaskTests
    {
        private readonly Mock<IInboxCleanupRepository> _inbox = new();

        [Fact]
        public async Task ExecuteAsync_CleanupDisabled_ReportsSuccessSkippedNoDeletion()
        {
            var context = new JobExecutionContext(null, "inbox-disabled");

            await new InboxCleanupTask(_inbox.Object, new MessagingOptions { CleanupEnabled = false })
                .ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("disabled");
            _inbox.Verify(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_DeletesProcessedOlderThanRetention()
        {
            _inbox.Setup(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            var context = new JobExecutionContext(null, "inbox-delete");

            await new InboxCleanupTask(_inbox.Object, new MessagingOptions()).ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("2");
            _inbox.Verify(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_RepositoryThrows_ReportsFailureNotException()
        {
            _inbox.Setup(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB connection failed"));
            var context = new JobExecutionContext(null, "inbox-error");

            await new InboxCleanupTask(_inbox.Object, new MessagingOptions()).ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>().Which.Error.Should().Contain("DB connection failed");
        }
    }
}