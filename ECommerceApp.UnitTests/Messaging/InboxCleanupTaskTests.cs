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

        private InboxCleanupTask CreateTask(bool cleanupEnabled = true)
            => new(_inbox.Object, new MessagingOptions { CleanupEnabled = cleanupEnabled });

        private void SetupDeletionResult(int deletedCount)
        {
            _inbox.Setup(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deletedCount);
        }

        private void SetupDeletionFailure(string message)
        {
            _inbox.Setup(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(message));
        }

        [Fact]
        public async Task ExecuteAsync_CleanupDisabled_ReportsSuccessSkippedNoDeletion()
        {
            // Arrange
            var context = new JobExecutionContext(null, "inbox-disabled");

            // Act
            await CreateTask(cleanupEnabled: false).ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("disabled");
            _inbox.Verify(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_DeletesProcessedOlderThanRetention()
        {
            // Arrange
            SetupDeletionResult(2);
            var context = new JobExecutionContext(null, "inbox-delete");

            // Act
            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("2");
            _inbox.Verify(r => r.DeleteProcessedOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_RepositoryThrows_ReportsFailureNotException()
        {
            // Arrange
            SetupDeletionFailure("DB connection failed");
            var context = new JobExecutionContext(null, "inbox-error");

            // Act
            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>().Which.Error.Should().Contain("DB connection failed");
        }
    }
}