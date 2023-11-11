using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services.Currencies
{
    public interface ICurrencyService : IAbstractService<CurrencyVm, ICurrencyRepository, Currency>
    {
        List<CurrencyVm> GetAll(Expression<Func<Currency, bool>> expression);
        ListCurrencyVm GetAllCurrencies(int pageSize, int pageNo, string searchString);
    }
}
