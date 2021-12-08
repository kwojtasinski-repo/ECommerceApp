using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.External.Client
{
    public interface INBPClient
    {
        Task<string> GetCurrency(string currencyCode, CancellationToken cancellationToken);
        Task<string> GetCurrencyRateOnDate(string currencyCode, DateTime dateTime, CancellationToken cancellationToken);
        Task<string> GetCurrencyTable(CancellationToken cancellationToken);
    }
}
