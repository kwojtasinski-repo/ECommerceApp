using ECommerceApp.Application.External.Client;
using ECommerceApp.Shared.TestInfrastructure;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Currencies
{
    /// <summary>Deterministic tests against a canned NBP response (no network).</summary>
    public class NBPClientTests : NbpStubBaseTest
    {
        public NBPClientTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task given_valid_currency_should_return_rate()
        {
            var code = "eUR";

            var rate = await _service.GetCurrency(code, CancellationToken.None);

            rate.ShouldNotBeNull();
            var exchangeRate = JsonConvert.DeserializeObject<ExchangeRate>(rate);
            exchangeRate.ShouldNotBeNull();
            exchangeRate.Rates.ShouldNotBeEmpty();
            exchangeRate.Rates[0].Mid.ShouldBe(4.25m);
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

    /// <summary>
    /// Contract test against the real api.nbp.pl. Excluded from the default run; execute explicitly with
    /// <c>dotnet test --filter "Category=ExternalApi"</c>.
    /// </summary>
    [Trait("Category", "ExternalApi")]
    public class NBPClientContractTests : BaseTest<INBPClient>
    {
        public NBPClientContractTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task real_nbp_should_return_currency_table_with_positive_rates()
        {
            var table = await _service.GetCurrencyTable(CancellationToken.None);

            var tables = JsonConvert.DeserializeObject<List<ExchangeRate>>(table);
            tables.ShouldNotBeNull();
            tables.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task real_nbp_should_return_eur_rate()
        {
            var rate = await _service.GetCurrency("eur", CancellationToken.None);

            rate.ShouldNotBeNull();
            var exchangeRate = JsonConvert.DeserializeObject<ExchangeRate>(rate);
            exchangeRate.Rates[0].Mid.ShouldBeGreaterThan(0);
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
