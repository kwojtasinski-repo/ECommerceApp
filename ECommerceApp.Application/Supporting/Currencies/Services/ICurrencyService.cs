using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    public interface ICurrencyService
    {
        Task<int> AddAsync(CreateCurrencyDto dto);
        Task<bool> UpdateAsync(UpdateCurrencyDto dto);
        Task<bool> DeleteAsync(int id);
        Task<CurrencyVm> GetByIdAsync(int id);
        Task<List<CurrencyVm>> GetAllAsync();
        Task<CurrencyListVm> GetAllAsync(int pageSize, int pageNo, string searchString);
    }
}
