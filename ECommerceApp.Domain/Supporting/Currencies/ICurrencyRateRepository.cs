using System;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public interface ICurrencyRateRepository
    {
        Task<CurrencyRateId> AddAsync(CurrencyRate currencyRate);
        Task<CurrencyRate> GetRateForDateAsync(CurrencyId currencyId, DateTime date);
    }
}
