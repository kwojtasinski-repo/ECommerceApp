using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using Xunit;

namespace ECommerceApp.UnitTests.Services.CurrencyRate
{
    public class CurrencyRateServiceTests : BaseTest
    {
        private readonly Mock<ICurrencyRateRepository> _currencyRateRepository;
        private readonly Mock<ICurrencyRepository> _currencyRepository;
        private readonly Mock<INBPClient> _NBPClient;

        public CurrencyRateServiceTests()
        {
            _currencyRateRepository = new Mock<ICurrencyRateRepository>();
            _currencyRepository = new Mock<ICurrencyRepository>();
            _NBPClient = new Mock<INBPClient>();
        }

        [Fact]
        public void given_currency_pln_and_date_when_getting_rate_for_day_should_return_rate()
        {
            int currencyId = 1;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetRateForDay(currencyId, date);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
            _currencyRateRepository.Verify(cr => cr.Add(It.IsAny<Domain.Model.CurrencyRate>()), Times.Once);
        }

        [Fact]
        public void given_currency_eur_and_date_when_getting_rate_for_day_should_return_rate()
        {
            int currencyId = 2;
            var code = "EUR";
            var date = DateTime.Now;
            var currency = GetCurrency(currencyId, code);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            _NBPClient.Setup(n => n.GetCurrencyRateOnDate(code, date.Date, CancellationToken.None)).ReturnsAsync(GetDefaultContentMessage());
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetRateForDay(currencyId, date);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
            _currencyRateRepository.Verify(cr => cr.Add(It.IsAny<Domain.Model.CurrencyRate>()), Times.Once);
        }

        [Fact]
        public void given_default_currency_and_invalid_date_when_getting_rate_for_day_should_throw_an_exception()
        {
            int currencyId = 1;
            var date = DateTime.Now.AddYears(-1000);
            var currency = GetDefaultCurrency(currencyId);
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"There is no rate for {date}");

            Action action = () => { currencyRateService.GetRateForDay(currencyId, date); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_valid_currency_and_proper_date_and_invalid_url_when_getting_rate_for_day_should_throw_an_exception()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"Check currency code {currency.Code} if is valid");

            Action action = () => { currencyRateService.GetRateForDay(currencyId, date); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_currency_and_proper_date_when_getting_rate_for_day_should_throw_an_exception()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"Currency with id: {currencyId} not found");

            Action action = () => { currencyRateService.GetRateForDay(currencyId, date); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_invalid_currency_when_getting_latest_rate_should_throw_an_exception()
        {
            var currencyId = 1;
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"Currency with id: {currencyId} not found");

            Action action = () => { currencyRateService.GetLatestRate(currencyId); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        [Fact]
        public void given_currency_pln_and_date_when_getting_latest_rate_should_return_rate()
        {
            int currencyId = 1;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            _currencyRateRepository.Setup(c => c.GetRateForDate(It.IsAny<int>(), It.IsAny<DateTime>())).Returns(new Domain.Model.CurrencyRate() { CurrencyId = currencyId, Rate = 1 });
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetLatestRate(currencyId);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
        }

        [Fact]
        public void given_currency_eur_and_date_when_getting_latest_rate_should_return_rate()
        {
            int currencyId = 2;
            var code = "EUR";
            var date = DateTime.Now;
            var currency = GetCurrency(currencyId, code);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            _currencyRateRepository.Setup(c => c.GetRateForDate(It.IsAny<int>(), It.IsAny<DateTime>())).Returns(new Domain.Model.CurrencyRate() { Id = 1, CurrencyId = currencyId, Rate = 0.5M });
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetLatestRate(currencyId);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
        }

        [Fact]
        public void given_valid_currency_and_proper_date_and_invalid_url_when_getting_latest_rate_should_throw_an_exception()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetById(currencyId)).Returns(currency);
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"Check currency code {currency.Code} if is valid");

            Action action = () => { currencyRateService.GetRateForDay(currencyId, date); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        private static Domain.Model.Currency GetDefaultCurrency(int currencyId)
        {
            var currency = new Domain.Model.Currency
            {
                Id = currencyId,
                Code = "PLN",
                Description = "Polski złoty"
            };
            return currency;
        }

        private static Domain.Model.Currency GetCurrency(int currencyId, string code)
        {
            var currency = new Domain.Model.Currency
            {
                Id = currencyId,
                Code = code.ToUpper(),
                Description = ""
            };
            return currency;
        }

        private static string GetDefaultContentMessage()
        {
            return "{\"table\":\"A\",\"currency\":\"euro\",\"code\":\"EUR\",\"rates\":[{\"no\":\"243/A/NBP/2021\",\"effectiveDate\":\"2021-12-16\",\"mid\":4.6315}]}";
        }
    }
}
