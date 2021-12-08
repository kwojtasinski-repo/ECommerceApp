using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class CurrencyService : AbstractService<CurrencyVm, ICurrencyRepository, Currency>, ICurrencyService
    {
        public CurrencyService(ICurrencyRepository currencyRepository, IMapper mapper) : base(currencyRepository, mapper)
        {
        }

        public List<CurrencyVm> GetAll(Expression<Func<Currency, bool>> expression)
        {
            List<Currency> currencies = _repo.GetAll(expression);
            List<CurrencyVm> currencyVms = new List<CurrencyVm>();

            foreach(Currency currency in currencies)
            {
                var currencyVm = _mapper.Map<CurrencyVm>(currency);
                currencyVms.Add(currencyVm);
            }

            return currencyVms;
        }
    }
}
