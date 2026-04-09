using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeCurrencyService
    {
        Task<BackofficeCurrencyListVm> GetCurrenciesAsync(CancellationToken ct = default);
        Task<BackofficeCurrencyDetailVm?> GetCurrencyDetailAsync(int currencyId, CancellationToken ct = default);
    }
}
