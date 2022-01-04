using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.CurrencyRate;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CurrencyRateServiceTests : BaseTest<ICurrencyRateService>
    {
        [Fact]
        public void given_currency_and_date_should_return_latest_rate_for_date()
        {
            var date = DateTime.Now;
            var currencyId = 4;

            var currencyRate = _service.GetRateForDay(currencyId, date);

            currencyRate.ShouldNotBeNull();
            currencyRate.CurrencyId.ShouldBe(currencyId);
            currencyRate.Rate.ShouldBeGreaterThan(0);
            currencyRate.CurrencyDate.Date.ShouldBeLessThan(date);
            currencyRate.CurrencyDate.Date.ShouldBeGreaterThan(date.Date.AddDays(-5));
        }

        [Fact]
        public void given_currency_id_should_return_latest_rate()
        {
            var date = DateTime.Now;
            var currencyId = 4;
            
            var currencyRate = _service.GetLatestRate(currencyId);

            currencyRate.ShouldNotBeNull();
            currencyRate.Rate.ShouldBeGreaterThan(0);
            currencyRate.CurrencyDate.ShouldBeLessThan(date);
            currencyRate.CurrencyDate.ShouldBeGreaterThan(date.Date.AddDays(-5));
        }
    }
}
