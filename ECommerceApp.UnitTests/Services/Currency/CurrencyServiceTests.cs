using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Currency
{
    public class CurrencyServiceTests : BaseTest
    {
        private readonly Mock<ICurrencyRepository> _currencyRepository;

        public CurrencyServiceTests()
        {
            _currencyRepository = new Mock<ICurrencyRepository>();
        }

        [Fact]
        public void given_valid_currency_should_add()
        {
            var currency = CreateCurrencyVm(0);
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            currencyService.Add(currency);

            _currencyRepository.Verify(c => c.Add(It.IsAny<Domain.Model.Currency>()), Times.Once);
        }

        [Fact]
        public void given_invalid_currency_should_throw_an_exception()
        {
            var currency = CreateCurrencyVm(0);
            currency.Code = "";
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Add(currency);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Code shouldnt be empty");
        }

        [Fact]
        public void given_valid_exists_currency_should_throw_an_exception()
        {
            var currency = CreateCurrencyVm(1);
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Add(currency);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_currency_should_update()
        {
            var currency = CreateCurrencyVm(1);
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            currencyService.Update(currency);

            _currencyRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Currency>()), Times.Once);
        }

        [Fact]
        public void given_invalid_currency_code_should_throw_an_exception()
        {
            var currency = CreateCurrencyVm(1);
            currency.Code = "";
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Update(currency);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Code shouldnt be empty");
        }

        [Fact]
        public void given_null_currency_when_add_should_throw_an_exception()
        {
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Add(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_currency_when_update_should_throw_an_exception()
        {
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Update(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private CurrencyVm CreateCurrencyVm(int id)
        {
            return new CurrencyVm
            {
                Id = id,
                Code = "Afsdgs@4235",
                Description = "Description"
            };
        }
    }
}
