using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRateRepository
    {
        CurrencyRate GetById(int id);
        List<CurrencyRate> GetAll(Expression<Func<CurrencyRate, bool>> expression);
        void Update(CurrencyRate currencyRate);
        void Delete(CurrencyRate currencyRate);
        void Delete(int id);
        int Add(CurrencyRate currencyRate);
    }
}
