using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    public interface ICurrencyRateService
    {
        Task<CurrencyRateVm> GetLatestRateAsync(int currencyId);
        Task<CurrencyRateVm> GetRateForDayAsync(int currencyId, DateTime dateTime);
    }
}
