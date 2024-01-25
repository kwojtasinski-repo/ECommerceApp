using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
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
            var currency = CreateCurrencyDto(0);
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            currencyService.Add(currency);

            _currencyRepository.Verify(c => c.Add(It.IsAny<Domain.Model.Currency>()), Times.Once);
        }

        [Fact]
        public void given_invalid_currency_should_throw_an_exception()
        {
            var currency = CreateCurrencyDto(0);
            currency.Code = "";
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            Action action = () => currencyService.Add(currency);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Code shouldnt be empty");
        }

        [Fact]
        public void given_valid_currency_should_update()
        {
            var currency = AddCurrency(CreateCurrencyDto(1));
            var currencyService = new CurrencyService(_currencyRepository.Object, _mapper);

            currencyService.Update(currency);

            _currencyRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Currency>()), Times.Once);
        }

        [Fact]
        public void given_invalid_currency_code_should_throw_an_exception()
        {
            var currency = CreateCurrencyDto(1);
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

        private CurrencyDto AddCurrency(CurrencyDto currencyDto)
        {
            var currency = new Domain.Model.Currency { Id = currencyDto.Id, Code = currencyDto.Code, Description = currencyDto.Description };
            _currencyRepository.Setup(c => c.GetById(currency.Id)).Returns(currency);
            return currencyDto;
        }

        private static CurrencyDto CreateCurrencyDto(int id)
        {
            return new CurrencyDto
            {
                Id = id,
                Code = "Afsdgs@4235",
                Description = "Description"
            };
        }
    }
}
