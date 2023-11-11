using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class CurrencyService : AbstractService<CurrencyVm, ICurrencyRepository, Currency>, ICurrencyService
    {
        public CurrencyService(ICurrencyRepository currencyRepository, IMapper mapper) : base(currencyRepository, mapper)
        {
        }

        public override int Add(CurrencyVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(CurrencyVm).Name} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(vm.Code))
            {
                throw new BusinessException("Code shouldnt be empty");
            }

            vm.Code = vm.Code.ToUpper();
            return base.Add(vm);
        }

        public override void Update(CurrencyVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{typeof(CurrencyVm).Name} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(vm.Code))
            {
                throw new BusinessException("Code shouldnt be empty");
            }

            vm.Code = vm.Code.ToUpper();
            base.Update(vm);
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

        public ListCurrencyVm GetAllCurrencies(int pageSize, int pageNo, string searchString)
        {
            var currencies = _repo.GetAll().Where(c => c.Code.StartsWith(searchString));
            List<Currency> currenciesToShow = currencies.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
            List<CurrencyVm> currencyVms = new List<CurrencyVm>();

            foreach (Currency currency in currenciesToShow)
            {
                var currencyVm = _mapper.Map<CurrencyVm>(currency);
                currencyVms.Add(currencyVm);
            }

            var listCurrency = new ListCurrencyVm
            {
                PageSize = pageSize,
                Currencies = currencyVms,
                CurrentPage = pageNo,
                SearchString = searchString,
                Count = currencies.Count()
            };

            return listCurrency;
        }
    }
}
