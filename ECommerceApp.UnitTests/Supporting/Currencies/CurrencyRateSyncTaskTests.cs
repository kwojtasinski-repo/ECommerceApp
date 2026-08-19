using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Currencies
{
    public class CurrencyRateSyncTaskTests
    {
        private readonly Mock<ICurrencyRateService> _currencyRateService;
        private readonly CurrencyRateSyncTask _task;

        public CurrencyRateSyncTaskTests()
        {
            _currencyRateService = new Mock<ICurrencyRateService>();
            _task = new CurrencyRateSyncTask(_currencyRateService.Object);
        }

        private void SetupSyncResult(int syncedCount)
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(syncedCount);
        }

        private void SetupSyncFailure(string message)
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BusinessException(message));
        }

        [Fact]
        public void TaskName_ShouldBeCurrencyDownloader()
        {
            // Arrange Act Assert
            _task.TaskName.Should().Be("CurrencyDownloader");
        }

        [Fact]
        public async Task ExecuteAsync_SyncsAllRatesInSingleBatchCall()
        {
            // Arrange
            SetupSyncResult(3);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await _task.ExecuteAsync(context, TestContext.Current.CancellationToken);

            // Assert
            _currencyRateService.Verify(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NeverCallsGetLatestRateAsync()
        {
            // Arrange
            SetupSyncResult(2);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await _task.ExecuteAsync(context, TestContext.Current.CancellationToken);

            // Assert
            _currencyRateService.Verify(s => s.GetLatestRateAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ReportsSuccessWithSyncedCount()
        {
            // Arrange
            SetupSyncResult(3);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await _task.ExecuteAsync(context, TestContext.Current.CancellationToken);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("3");
        }

        [Fact]
        public async Task ExecuteAsync_ZeroRatesSynced_ReportsSuccessWithZero()
        {
            // Arrange
            SetupSyncResult(0);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await _task.ExecuteAsync(context, TestContext.Current.CancellationToken);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("0");
        }

        [Fact]
        public async Task ExecuteAsync_ServiceThrows_ReportsFailure()
        {
            // Arrange
            SetupSyncFailure("NBP unavailable");
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            // Act
            await _task.ExecuteAsync(context, TestContext.Current.CancellationToken);

            // Assert
            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("NBP unavailable");
        }
    }
}
