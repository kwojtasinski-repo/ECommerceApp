using ECommerceApp.Infrastructure.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.External.Client
{
    public class NBPClient : INBPClient
    {
        private string _baseUrl = "http://api.nbp.pl";
        private readonly HttpClient _httpClient;

        public NBPClient(IHttpClientFactory factory)
        {
            _httpClient = factory.CreateClient("NBPClient");
        }

        public async Task<string> GetCurrency(string currencyCode, CancellationToken cancellationToken)
        {
            var urlBuilder = new StringBuilder();
            urlBuilder.Append(_baseUrl.TrimEnd('/')).Append("/api/exchangerates/rates/a/");
            urlBuilder.Append(currencyCode.ToLower());
            var content = await SendGetRequest(urlBuilder.ToString(), cancellationToken);
            return content;
        }

        public async Task<string> GetCurrencyRateOnDate(string currencyCode, DateTime dateTime, CancellationToken cancellationToken)
        {
            var urlBuilder = new StringBuilder();
            urlBuilder.Append(_baseUrl.TrimEnd('/')).Append("/api/exchangerates/rates/a/");
            urlBuilder.Append(currencyCode.ToLower()).Append("/");
            urlBuilder.Append(dateTime.ToString("yyyy-MM-dd"));
            var content = await SendGetRequest(urlBuilder.ToString(), cancellationToken);
            return content;
        }

        public async Task<string> GetCurrencyTable(CancellationToken cancellationToken)
        {
            var urlBuilder = new StringBuilder();
            urlBuilder.Append(_baseUrl.TrimEnd('/')).Append("/api/exchangerates/tables/a");
            var content = await SendGetRequest(urlBuilder.ToString(), cancellationToken);
            return content;
        }

        private async Task<string> SendGetRequest(string urlBuilder, CancellationToken cancellationToken)
        {
            var client = _httpClient;
            try
            {
                using (var request = new HttpRequestMessage())
                {
                    request.Method = new HttpMethod("GET");
                    var url = urlBuilder;
                    request.RequestUri = new Uri(url, UriKind.RelativeOrAbsolute);
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var responseData = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return responseData;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else
                    {
                        throw new InfrastructureException($"Check base url: {urlBuilder}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InfrastructureException($"Check if url {urlBuilder} is entered correctly or service is unavaible", ex);
            }
        }
    }
}
