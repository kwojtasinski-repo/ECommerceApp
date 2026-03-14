using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using FluentAssertions;
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

        [Fact]
        public void TaskName_ShouldBeCurrencyDownloader()
        {
            _task.TaskName.Should().Be("CurrencyDownloader");
        }

        [Fact]
        public async Task ExecuteAsync_SyncsAllRatesInSingleBatchCall()
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _task.ExecuteAsync(context, default);

            _currencyRateService.Verify(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NeverCallsGetLatestRateAsync()
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _task.ExecuteAsync(context, default);

            _currencyRateService.Verify(s => s.GetLatestRateAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ReportsSuccessWithSyncedCount()
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _task.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("3");
        }

        [Fact]
        public async Task ExecuteAsync_ZeroRatesSynced_ReportsSuccessWithZero()
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _task.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("0");
        }

        [Fact]
        public async Task ExecuteAsync_ServiceThrows_ReportsFailure()
        {
            _currencyRateService.Setup(s => s.SyncAllRatesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BusinessException("NBP unavailable"));
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _task.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("NBP unavailable");
        }
    }
}
