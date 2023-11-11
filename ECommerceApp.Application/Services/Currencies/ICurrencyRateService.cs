using ECommerceApp.Application.ViewModels.CurrencyRate;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Services.Currencies
{
    public interface ICurrencyRateService
    {
        CurrencyRateVm GetLatestRate(int currencyId);
        CurrencyRateVm GetRateForDay(int currencyId, DateTime dateTime);
    }
}
