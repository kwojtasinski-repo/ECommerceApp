using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Supporting.Currencies.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCurrencyService : IBackofficeCurrencyService
    {
        private readonly ICurrencyService _currencyService;

        public BackofficeCurrencyService(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        public async Task<BackofficeCurrencyListVm> GetCurrenciesAsync(CancellationToken ct = default)
        {
            var currencies = await _currencyService.GetAllAsync();
            return new BackofficeCurrencyListVm
            {
                Currencies = currencies.Select(c => new BackofficeCurrencyItemVm
                {
                    Id = c.Id,
                    Code = c.Code,
                    Description = c.Description
                }).ToList()
            };
        }

        public async Task<BackofficeCurrencyDetailVm> GetCurrencyDetailAsync(int currencyId, CancellationToken ct = default)
        {
            var vm = await _currencyService.GetByIdAsync(currencyId);
            if (vm is null)
                return null;

            return new BackofficeCurrencyDetailVm
            {
                Id = vm.Id,
                Code = vm.Code,
                Description = vm.Description,
                Rates = new List<BackofficeCurrencyRateVm>()
            };
        }
    }
}

