using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRateRepository
    {
        CurrencyRate GetById(int id);
        List<CurrencyRate> GetAll();
        CurrencyRate GetRateForDate(int currencyId, DateTime date);
        void Update(CurrencyRate currencyRate);
        void Delete(CurrencyRate currencyRate);
        void Delete(int id);
        int Add(CurrencyRate currencyRate);
    }
}
