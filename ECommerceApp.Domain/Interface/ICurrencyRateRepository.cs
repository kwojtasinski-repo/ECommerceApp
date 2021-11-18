using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRateRepository : IGenericRepository<CurrencyRate>
    {
        decimal GetRateForDay(DateTime dateTime);
    }
}
