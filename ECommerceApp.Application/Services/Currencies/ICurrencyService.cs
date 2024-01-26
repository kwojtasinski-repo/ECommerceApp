using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Currency;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Currencies
{
    public interface ICurrencyService
    {
        List<CurrencyDto> GetAll();
        CurrencyDto GetById(int id);
        int Add(CurrencyDto dto);
        void Update(CurrencyDto dto);
        void Delete(int id);
        ListCurrencyVm GetAllCurrencies(int pageSize, int pageNo, string searchString);
    }
}
