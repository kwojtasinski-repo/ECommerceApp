using ECommerceApp.Application.Constants;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.UnitTests.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
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
                _nbpClient.Object,
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new CacheOptions()));
        }

        private void SetupCurrency(string code, Currency currency)
        {
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
        }

        private void SetupRateLookup(CurrencyRate rate)
        {
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync(rate);
        }

        private void SetupNbpRate(string code, DateTime date, string response)
        {
            _nbpClient.Setup(n => n.GetCurrencyRateOnDate(code, date.Date, CancellationToken.None)).ReturnsAsync(response);
        }

        [Fact]
        public async Task GetRateForDayAsync_PlnCurrency_ShouldReturnRateOne()
        {
            int currencyId = 1;
            var date = DateTime.Now;
            var currency = Currency.Create("PLN", "Polish zloty");
            SetupCurrency("PLN", currency);
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
            SetupCurrency("PLN", currency);
            SetupRateLookup(existingRate);

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
            SetupCurrency("EUR", currency);
            SetupNbpRate("EUR", date, GetDefaultNbpResponse());
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
            SetupCurrency("", null);

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
            SetupCurrency("XXX", currency);
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
            SetupCurrency("PLN", currency);
            SetupRateLookup(existingRate);

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
            SetupCurrency("EUR", currency);
            SetupRateLookup(existingRate);

            var result = await _sut.GetLatestRateAsync(currencyId);

            result.Should().NotBeNull();
            result.CurrencyId.Should().Be(currencyId);
            result.Rate.Should().Be(4.5m);
        }

        [Fact]
        public async Task GetLatestRateAsync_NonExistentCurrency_ShouldThrowBusinessException()
        {
            int currencyId = 999;
            SetupCurrency("", null);

            Func<Task> action = () => _sut.GetLatestRateAsync(currencyId);

            await action.Should().ThrowAsync<BusinessException>()
                .WithMessage($"Currency with id: {currencyId} not found");
        }

        private static string GetDefaultNbpResponse()
        {
            return "{\"table\":\"A\",\"currency\":\"euro\",\"code\":\"EUR\",\"rates\":[{\"no\":\"243/A/NBP/2021\",\"effectiveDate\":\"2021-12-16\",\"mid\":4.6315}]}";
        }

        private static string GetDefaultTableResponse(string code = "EUR", decimal mid = 4.5m)
        {
            return $"[{{\"table\":\"A\",\"no\":\"001/A/NBP/2024\",\"effectiveDate\":\"2024-01-02\",\"rates\":[{{\"currency\":\"euro\",\"code\":\"{code}\",\"mid\":{mid.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}]";
        }

        private static Currency CurrencyWithId(int id, string code, string description)
        {
            var currency = Currency.Create(code, description);
            EntityIdSetter.Set(currency, new CurrencyId(id));
            return currency;
        }

        private void SetupCurrencies(params Currency[] currencies)
        {
            _currencyRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Currency>(currencies));
        }

        private void SetupNbpTable(string response)
        {
            _nbpClient.Setup(n => n.GetCurrencyTable(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
        }

        private void SetupNoRateForDate()
        {
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync((CurrencyRate)null);
        }

        private void SetupRatePersistence()
        {
            _currencyRateRepository.Setup(r => r.AddAsync(It.IsAny<CurrencyRate>()))
                .ReturnsAsync(new CurrencyRateId(1));
        }

        // ── SyncAllRatesAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task SyncAllRatesAsync_TableNotAvailable_ShouldReturnZero()
        {
            SetupNbpTable(null);

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(0);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllRatesAsync_EmptyTableResponse_ShouldReturnZero()
        {
            SetupNbpTable("[]");

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(0);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllRatesAsync_PlnOnlyCurrencies_ShouldSkipAllAndReturnZero()
        {
            var pln = Currency.Create("PLN", "Polish zloty");
            EntityIdSetter.Set(pln, new CurrencyId(1));
            SetupCurrencies(pln);
            SetupNbpTable(GetDefaultTableResponse("EUR", 4.5m));

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(0);
        }

        [Fact]
        public async Task SyncAllRatesAsync_CurrencyNotInTable_ShouldSkipAndReturnZero()
        {
            var usd = CurrencyWithId(3, "USD", "US Dollar");
            SetupCurrencies(usd);
            SetupNbpTable(GetDefaultTableResponse("EUR", 4.5m)); // table has EUR, not USD

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(0);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllRatesAsync_RateAlreadyExistsForToday_ShouldSkipAndReturnZero()
        {
            var eur = CurrencyWithId(2, "EUR", "Euro");
            var existingRate = CurrencyRate.Create(new CurrencyId(2), 4.5m, DateTime.UtcNow.Date);
            SetupCurrencies(eur);
            SetupNbpTable(GetDefaultTableResponse("EUR", 4.5m));
            _currencyRateRepository.Setup(r => r.GetRateForDateAsync(It.IsAny<CurrencyId>(), It.IsAny<DateTime>()))
                .ReturnsAsync(existingRate);

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(0);
            _currencyRateRepository.Verify(r => r.AddAsync(It.IsAny<CurrencyRate>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllRatesAsync_NewRateInTable_ShouldPersistAndReturnOne()
        {
            var eur = CurrencyWithId(2, "EUR", "Euro");
            SetupCurrencies(eur);
            SetupNbpTable(GetDefaultTableResponse("EUR", 4.25m));
            SetupNoRateForDate();
            SetupRatePersistence();

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(1);
            _currencyRateRepository.Verify(r => r.AddAsync(
                It.Is<CurrencyRate>(cr => cr.Rate == 4.25m)), Times.Once);
        }

        [Fact]
        public async Task SyncAllRatesAsync_IssuedOneCallToNbp_RegardlessOfCurrencyCount()
        {
            var currencies = new List<Currency>
            {
                CurrencyWithId(2, "EUR", "Euro"),
                CurrencyWithId(3, "USD", "US Dollar"),
                CurrencyWithId(4, "GBP", "British Pound")
            };
            SetupCurrencies(currencies.ToArray());
            SetupNbpTable("[{\"table\":\"A\",\"no\":\"001\",\"effectiveDate\":\"2024-01-02\",\"rates\":[{\"currency\":\"euro\",\"code\":\"EUR\",\"mid\":4.25},{\"currency\":\"dollar\",\"code\":\"USD\",\"mid\":3.95},{\"currency\":\"pound\",\"code\":\"GBP\",\"mid\":5.10}]}]");
            SetupNoRateForDate();
            SetupRatePersistence();

            var result = await _sut.SyncAllRatesAsync(TestContext.Current.CancellationToken);

            result.Should().Be(3);
            _nbpClient.Verify(n => n.GetCurrencyTable(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
