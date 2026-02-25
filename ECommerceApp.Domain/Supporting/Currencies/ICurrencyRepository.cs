using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public interface ICurrencyRepository
    {
        Task<CurrencyId> AddAsync(Currency currency);
        Task<Currency> GetByIdAsync(CurrencyId id);
        Task UpdateAsync(Currency currency);
        Task<bool> DeleteAsync(CurrencyId id);
        Task<List<Currency>> GetAllAsync();
        Task<List<Currency>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountBySearchStringAsync(string searchString);
    }
}
