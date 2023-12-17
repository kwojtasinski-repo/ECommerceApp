using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Currencies
{
    public interface ICurrencyService
    {
        List<CurrencyDto> GetAll(Expression<Func<Currency, bool>> expression);
        CurrencyDto GetById(int id);
        int Add(CurrencyDto dto);
        void Update(CurrencyDto dto);
        void Delete(int id);
        ListCurrencyVm GetAllCurrencies(int pageSize, int pageNo, string searchString);
    }
}
