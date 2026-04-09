using ECommerceApp.Application.Backoffice.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCurrencyService : IBackofficeCurrencyService
    {
        public Task<BackofficeCurrencyListVm> GetCurrenciesAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeCurrencyDetailVm?> GetCurrencyDetailAsync(int currencyId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
