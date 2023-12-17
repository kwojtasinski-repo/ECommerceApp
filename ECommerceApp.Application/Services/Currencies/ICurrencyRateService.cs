using ECommerceApp.Application.DTO;
using System;

namespace ECommerceApp.Application.Services.Currencies
{
    public interface ICurrencyRateService
    {
        CurrencyRateDto GetLatestRate(int currencyId);
        CurrencyRateDto GetRateForDay(int currencyId, DateTime dateTime);
    }
}
