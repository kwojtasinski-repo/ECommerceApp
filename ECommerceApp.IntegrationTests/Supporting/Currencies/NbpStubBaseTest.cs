using ECommerceApp.Application.External.Client;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Currencies
{
    /// <summary>
    /// Test host whose "NBPClient" HttpClient talks to a canned in-process handler instead of api.nbp.pl,
    /// so tests are deterministic and offline.
    /// </summary>
    public abstract class NbpStubBaseTest : BaseTest<INBPClient>
    {
        private const string RateJson =
            "{\"table\":\"A\",\"currency\":\"euro\",\"code\":\"EUR\",\"rates\":[{\"no\":\"001/A/NBP/2026\",\"effectiveDate\":\"2026-01-02\",\"mid\":4.25}]}";

        private const string TableJson =
            "[{\"table\":\"A\",\"no\":\"001/A/NBP/2026\",\"effectiveDate\":\"2026-01-02\",\"rates\":[{\"currency\":\"euro\",\"code\":\"EUR\",\"mid\":4.25}]}]";

        protected NbpStubBaseTest(ITestOutputHelper output) : base(output) { }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            base.OverrideServicesImplementation(services);
            services.AddHttpClient("NBPClient")
                .ConfigurePrimaryHttpMessageHandler(() => new CannedNbpHandler(RateJson, TableJson));
        }

        private sealed class CannedNbpHandler : HttpMessageHandler
        {
            private readonly string _rateJson;
            private readonly string _tableJson;

            public CannedNbpHandler(string rateJson, string tableJson)
            {
                _rateJson = rateJson;
                _tableJson = tableJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
                string body = null;
                if (path == "/api/exchangerates/tables/a")
                {
                    body = _tableJson;
                }
                else if (path == "/api/exchangerates/rates/a/eur")
                {
                    body = _rateJson;
                }

                if (body == null)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
