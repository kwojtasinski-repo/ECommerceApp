using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Currencies
{
    public class CurrencyRateServiceTests : BaseTest
    {
        private readonly Mock<ICurrencyRateRepository> _currencyRateRepository;
        private readonly Mock<ICurrencyRepository> _currencyRepository;
        private readonly Mock<INBPClient> _nbpClient;
        private readonly CurrencyRateService _sut;

        public CurrencyRateServiceTests()
        {
            _currencyRateRepository = new Mock<ICurrencyRateRepository>();
            _currencyRepository = new Mock<ICurrencyRepository>();
            _nbpClient = new Mock<INBPClient>();
            _sut = new CurrencyRateService(
                _currencyRateRepository.Object,
                _currencyRepository.Object,
                _mapper,
                _nbpClient.Object);
        }

        [Fact]
        public async Task GetRateForDayAsync_PlnCurrency_ShouldReturnRateOne()
        {
            int currencyId = 1;
            var date = DateTime.Now;
            var currency = Currency.Create("PLN", "Polish zloty");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _currencyRateRepository.Setup(r => r.AddAsync(It.IsAny<CurrencyRate>()))
                .ReturnsAsync(new CurrencyRateId(1));

            var result = await _sut.GetRateForDayAsync(currencyId, date);

            result.Should().NotBeNull();
            result.CurrencyId.Should().Be(currencyId);
            result.Rate.Should().Be(1.0m);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Once);
        }

        [Fact]
        public async Task GetRateForDayAsync_PlnCurrencyWithExistingRate_ShouldReturnCachedRate()
        {
            int currencyId = 1;
            var date = DateTime.Now;
            var currency = Currency.Create("PLN", "Polish zloty");
            var existingRate = CurrencyRate.Create(new CurrencyId(currencyId), 1.0m, date);
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync(existingRate);

            var result = await _sut.GetRateForDayAsync(currencyId, date);

            result.Should().NotBeNull();
            result.Rate.Should().Be(1.0m);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Never);
        }

        [Fact]
        public async Task GetRateForDayAsync_EurCurrency_ShouldFetchFromNbp()
        {
            int currencyId = 2;
            var date = DateTime.Now;
            var currency = Currency.Create("EUR", "Euro");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _nbpClient.Setup(n => n.GetCurrencyRateOnDate("EUR", date.Date, CancellationToken.None))
                .ReturnsAsync(GetDefaultNbpResponse());
            _currencyRateRepository.Setup(r => r.AddAsync(It.IsAny<CurrencyRate>()))
                .ReturnsAsync(new CurrencyRateId(1));

            var result = await _sut.GetRateForDayAsync(currencyId, date);

            result.Should().NotBeNull();
            result.Rate.Should().BeGreaterThan(0);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Once);
            _nbpClient.Verify(n => n.GetCurrencyRateOnDate("EUR", date.Date, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task GetRateForDayAsync_DateBeforeArchive_ShouldThrowBusinessException()
        {
            int currencyId = 1;
            var date = DateTime.Now.AddYears(-1000);

            Func<Task> action = () => _sut.GetRateForDayAsync(currencyId, date);

            await action.Should().ThrowAsync<BusinessException>()
                .WithMessage($"There is no rate for {date}");
        }

        [Fact]
        public async Task GetRateForDayAsync_NonExistentCurrency_ShouldThrowBusinessException()
        {
            int currencyId = 999;
            var date = DateTime.Now;
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync((Currency)null);

            Func<Task> action = () => _sut.GetRateForDayAsync(currencyId, date);

            await action.Should().ThrowAsync<BusinessException>()
                .WithMessage($"Currency with id: {currencyId} not found");
        }

        [Fact]
        public async Task GetRateForDayAsync_NbpReturnsNull_ShouldThrowAfterMaxRequests()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = Currency.Create("XXX", "Unknown");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _nbpClient.Setup(n => n.GetCurrencyRateOnDate(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);

            Func<Task> action = () => _sut.GetRateForDayAsync(currencyId, date);

            await action.Should().ThrowAsync<BusinessException>()
                .WithMessage($"Check currency code XXX if is valid");
        }

        [Fact]
        public async Task GetLatestRateAsync_PlnCurrency_ShouldReturnRate()
        {
            int currencyId = 1;
            var currency = Currency.Create("PLN", "Polish zloty");
            var existingRate = CurrencyRate.Create(new CurrencyId(currencyId), 1.0m, DateTime.Now);
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync(existingRate);

            var result = await _sut.GetLatestRateAsync(currencyId);

            result.Should().NotBeNull();
            result.CurrencyId.Should().Be(currencyId);
            result.Rate.Should().Be(1.0m);
        }

        [Fact]
        public async Task GetLatestRateAsync_EurCurrencyWithCachedRate_ShouldReturnCachedRate()
        {
            int currencyId = 2;
            var currency = Currency.Create("EUR", "Euro");
            var existingRate = CurrencyRate.Create(new CurrencyId(currencyId), 4.5m, DateTime.Now);
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync(existingRate);

            var result = await _sut.GetLatestRateAsync(currencyId);

            result.Should().NotBeNull();
            result.CurrencyId.Should().Be(currencyId);
            result.Rate.Should().Be(4.5m);
        }

        [Fact]
        public async Task GetLatestRateAsync_NonExistentCurrency_ShouldThrowBusinessException()
        {
            int currencyId = 999;
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync((Currency)null);

            Func<Task> action = () => _sut.GetLatestRateAsync(currencyId);

            await action.Should().ThrowAsync<BusinessException>()
                .WithMessage($"Currency with id: {currencyId} not found");
        }

        private static string GetDefaultNbpResponse()
        {
            return "{\"table\":\"A\",\"currency\":\"euro\",\"code\":\"EUR\",\"rates\":[{\"no\":\"243/A/NBP/2021\",\"effectiveDate\":\"2021-12-16\",\"mid\":4.6315}]}";
        }
    }
}
