using ECommerceApp.Application.Interfaces;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using Xunit;

namespace ECommerceApp.IntegrationTests
{
    public class CurrencyServiceTests : BaseTest<ICurrencyService>
    {
        [Fact]
        public void given_valid_id_should_return_currency()
        {
            var id = 2;
            var code = "EUR";

            var currency = _service.Get(id);

            currency.ShouldNotBeNull();
            currency.Code.ShouldBe(code);
        }
    }
}
