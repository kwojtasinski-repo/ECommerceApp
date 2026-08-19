using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Identity.IAM;
using AwesomeAssertions;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Identity.IAM
{
    public class RefreshTokenCleanupTaskTests
    {
        private readonly Mock<IRefreshTokenRepository> _refreshTokens;

        public RefreshTokenCleanupTaskTests()
        {
            _refreshTokens = new Mock<IRefreshTokenRepository>();
        }

        private RefreshTokenCleanupTask CreateTask() => new(_refreshTokens.Object);

        [Fact]
        public void TaskName_ShouldBeRefreshTokenCleanup()
        {
            // Arrange
            var task = CreateTask();

            // Act Assert
            task.TaskName.Should().Be("RefreshTokenCleanup");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCallDeleteExpiredAndReportSuccess()
        {
            // Arrange
            SetupExpiredTokenDeletion(3);
            var context = new JobExecutionContext(null, "exec-1");
            var task = CreateTask();

            // Act
            await task.ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("3");
            _refreshTokens.Verify(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoExpiredTokens_ShouldReportZero()
        {
            // Arrange
            SetupExpiredTokenDeletion(0);
            var context = new JobExecutionContext(null, "exec-2");
            var task = CreateTask();

            // Act
            await task.ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("0");
        }

        [Fact]
        public async Task ExecuteAsync_WhenRepositoryThrows_ShouldReportFailure()
        {
            // Arrange
            SetupExpiredTokenDeletionFailure("DB connection failed");
            var context = new JobExecutionContext(null, "exec-3");
            var task = CreateTask();

            // Act
            await task.ExecuteAsync(context, CancellationToken.None);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("DB connection failed");
        }

        private void SetupExpiredTokenDeletion(int deletedCount)
        {
            _refreshTokens.Setup(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deletedCount);
        }

        private void SetupExpiredTokenDeletionFailure(string message)
        {
            _refreshTokens.Setup(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception(message));
        }
    }
}
