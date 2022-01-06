using ECommerceApp.Application.External.Client;
using ECommerceApp.IntegrationTests.Common;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class NBPClientTests : BaseTest<INBPClient> 
    {
        [Fact]
        public async Task given_valid_currency_should_return_rate()
        {
            var code = "eUR";

            var rate = await _service.GetCurrency(code, CancellationToken.None);

            rate.ShouldNotBeNull();
            var exchangeRate = JsonConvert.DeserializeObject<ExchangeRate>(rate);
            exchangeRate.ShouldNotBeNull();
            exchangeRate.Rates.ShouldNotBeEmpty();
            exchangeRate.Rates[0].Mid.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_currency_shouldnt_return_rate()
        {
            var code = "eURO";

            var rate = await _service.GetCurrency(code, CancellationToken.None);

            rate.ShouldBeNull();
        }

        [Fact]
        public async Task should_return_currency_table()
        {
            var table = await _service.GetCurrencyTable(CancellationToken.None);

            var exchangeRate = JsonConvert.DeserializeObject<List<ExchangeRate>>(table);
            exchangeRate.ShouldNotBeNull();
            exchangeRate.ShouldNotBeEmpty();
        }
    }

    internal class ExchangeRate
    {
        public string Table { get; set; }
        public string Currency { get; set; }
        public string Code { get; set; }
        public List<Rate> Rates { get; set; }
    }

    internal class Rate
    {
        public string No { get; set; }
        public DateTime EffectiveDate { get; set; }
        public decimal Mid { get; set; }
    }
}
