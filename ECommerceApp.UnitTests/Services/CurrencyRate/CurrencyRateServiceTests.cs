using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using Xunit;

namespace ECommerceApp.UnitTests.Services.CurrencyRate
{
    public class CurrencyRateServiceTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<ICurrencyRateRepository> _currencyRateRepository;
        private readonly Mock<ICurrencyRepository> _currencyRepository;
        private readonly Mock<INBPClient> _NBPClient;

        public CurrencyRateServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
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
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
            _currencyRateRepository.Setup(c => c.Add(It.IsAny<Domain.Model.CurrencyRate>())).Verifiable();
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
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
            _currencyRateRepository.Setup(c => c.Add(It.IsAny<Domain.Model.CurrencyRate>())).Verifiable();
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
        public void given_valid_currency_and_proper_date_and_invalid_address_when_getting_rate_for_day_should_throw_an_exception()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
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
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
            _currencyRateRepository.Setup(c => c.Add(It.IsAny<Domain.Model.CurrencyRate>())).Verifiable();
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetLatestRate(currencyId);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
            _currencyRateRepository.Verify(cr => cr.Add(It.IsAny<Domain.Model.CurrencyRate>()), Times.Once);
        }

        [Fact]
        public void given_currency_eur_and_date_when_getting_latest_rate_should_return_rate()
        {
            int currencyId = 2;
            var code = "EUR";
            var date = DateTime.Now;
            var currency = GetCurrency(currencyId, code);
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
            _currencyRateRepository.Setup(c => c.Add(It.IsAny<Domain.Model.CurrencyRate>())).Verifiable();
            _NBPClient.Setup(n => n.GetCurrencyRateOnDate(code, date.Date, CancellationToken.None)).ReturnsAsync(GetDefaultContentMessage());
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);

            var currencyRate = currencyRateService.GetLatestRate(currencyId);

            currencyRate.Should().NotBeNull();
            currencyRate.CurrencyId.Should().Be(currencyId);
            _currencyRateRepository.Verify(cr => cr.Add(It.IsAny<Domain.Model.CurrencyRate>()), Times.Once);
        }

        [Fact]
        public void given_valid_currency_and_proper_date_and_invalid_address_when_getting_latest_rate_should_throw_an_exception()
        {
            int currencyId = 10;
            var date = DateTime.Now;
            var currency = GetDefaultCurrency(currencyId);
            _currencyRepository.Setup(c => c.GetAll()).Returns(new List<Currency> { currency }.AsQueryable());
            _currencyRateRepository.Setup(c => c.GetAll(It.IsAny<Expression<Func<Domain.Model.CurrencyRate, bool>>>())).Returns(new List<Domain.Model.CurrencyRate>());
            var currencyRateService = new CurrencyRateService(_currencyRateRepository.Object, _currencyRepository.Object, _mapper, _NBPClient.Object);
            var expectedException = new BusinessException($"Check currency code {currency.Code} if is valid");

            Action action = () => { currencyRateService.GetRateForDay(currencyId, date); };

            action.Should().Throw<BusinessException>().WithMessage(expectedException.Message);
        }

        private Currency GetDefaultCurrency(int currencyId)
        {
            var currency = new Currency();
            currency.Id = currencyId;
            currency.Code = "PLN";
            currency.Description = "Polski złoty";
            return currency;
        }

        private Currency GetCurrency(int currencyId, string code)
        {
            var currency = new Currency();
            currency.Id = currencyId;
            currency.Code = code.ToUpper();
            currency.Description = "";
            return currency;
        }

        private string GetDefaultContentMessage()
        {
            return "{\"table\":\"A\",\"currency\":\"euro\",\"code\":\"EUR\",\"rates\":[{\"no\":\"243/A/NBP/2021\",\"effectiveDate\":\"2021-12-16\",\"mid\":4.6315}]}";
        }
    }
}
