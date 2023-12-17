using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CurrencyServiceTests : BaseTest<ICurrencyService>
    {
        [Fact]
        public void given_valid_id_should_return_currency()
        {
            var id = 2;
            var code = "EUR";

            var currency = _service.GetById(id);

            currency.ShouldNotBeNull();
            currency.Code.ShouldBe(code);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_currency()
        {
            var id = 123;

            var currency = _service.GetById(id);

            currency.ShouldBeNull();
        }

        [Fact]
        public void given_valid_currency_should_add()
        {
            var currency = CreateCurrency(0);

            var id = _service.Add(currency);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_currency_when_add_should_throw_an_exception()
        {
            var currency = CreateCurrency(0);
            currency.Code = "";

            var exception = Should.Throw<BusinessException>(() => _service.Add(currency));

            exception.Message.ShouldBe("Code shouldnt be empty");
        }

        [Fact]
        public void given_valid_currency_should_update()
        {
            var id = 1;
            var code = "ABC";
            var currency = _service.GetById(id);
            currency.Code = code;

            _service.Update(currency);

            var currencyUpdated = _service.GetById(id);
            currencyUpdated.Code.ShouldBe(code);
        }

        [Fact]
        public void given_invalid_currency_when_update_should_throw_an_exception()
        {
            var currency = CreateCurrency(1);
            currency.Code = "";

            var exception = Should.Throw<BusinessException>(() => _service.Update(currency));

            exception.Message.ShouldBe("Code shouldnt be empty");
        }

        [Fact]
        public void given_valid_id_should_delete()
        {
            var currency = CreateCurrency(0);
            var id = _service.Add(currency);
            
            _service.Delete(id);

            var currencyDeleted = _service.GetById(id);
            currencyDeleted.ShouldBeNull();
        }

        [Fact]
        public void given_valid_parameters_should_return_currencies()
        {
            var currencies = _service.GetAllCurrencies(20, 1, "");

            currencies.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_currencies_in_db_should_return_currencies()
        {
            var currencies = _service.GetAll(c => true);

            currencies.Count.ShouldBeGreaterThan(0);
        }

        private static CurrencyDto CreateCurrency(int id)
        {
            var currency = new CurrencyDto
            {
                Id = id,
                Code = "TST",
                Description = ""
            };
            return currency;
        }
    }
}
