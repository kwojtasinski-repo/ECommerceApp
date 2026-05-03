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
            CreateTask().TaskName.Should().Be("RefreshTokenCleanup");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCallDeleteExpiredAndReportSuccess()
        {
            _refreshTokens.Setup(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
            var context = new JobExecutionContext(null, "exec-1");
            var task = CreateTask();

            await task.ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("3");
            _refreshTokens.Verify(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoExpiredTokens_ShouldReportZero()
        {
            _refreshTokens.Setup(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
            var context = new JobExecutionContext(null, "exec-2");
            var task = CreateTask();

            await task.ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("0");
        }

        [Fact]
        public async Task ExecuteAsync_WhenRepositoryThrows_ShouldReportFailure()
        {
            _refreshTokens.Setup(r => r.DeleteExpiredAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("DB connection failed"));
            var context = new JobExecutionContext(null, "exec-3");
            var task = CreateTask();

            await task.ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("DB connection failed");
        }
    }
}
