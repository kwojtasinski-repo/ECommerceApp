using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Currencies
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IMapper _mapper;
        private readonly ICurrencyRepository _currencyRepository;

        public CurrencyService(ICurrencyRepository currencyRepository, IMapper mapper)
        {
            _currencyRepository = currencyRepository;
            _mapper = mapper;
        }

        public int Add(CurrencyDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(CurrencyDto).Name} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new BusinessException("Code shouldnt be empty");
            }

            dto.Code = dto.Code.ToUpper();
            var currency = _mapper.Map<Currency>(dto);
            _currencyRepository.Add(currency);
            return currency.Id;
        }

        public void Update(CurrencyDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(CurrencyDto).Name} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new BusinessException("Code shouldnt be empty");
            }

            dto.Code = dto.Code.ToUpper();
            var currency = _currencyRepository.GetById(dto.Id)
                ?? throw new BusinessException($"Currency with id '{dto.Id}' not found");
            currency.Code = dto.Code;
            currency.Description = dto.Description;
            _currencyRepository.Update(currency);
        }

        public List<CurrencyDto> GetAll(Expression<Func<Currency, bool>> expression)
        {
            List<Currency> currencies = _currencyRepository.GetAll(expression);
            List<CurrencyDto> currencyVms = new ();

            foreach (Currency currency in currencies)
            {
                var currencyDto = _mapper.Map<CurrencyDto>(currency);
                currencyVms.Add(currencyDto);
            }

            return currencyVms;
        }

        public ListCurrencyVm GetAllCurrencies(int pageSize, int pageNo, string searchString)
        {
            var currencies = _currencyRepository.GetAll().Where(c => c.Code.StartsWith(searchString));
            List<Currency> currenciesToShow = currencies.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
            List<CurrencyDto> currencyVms = new ();

            foreach (Currency currency in currenciesToShow)
            {
                var currencyVm = _mapper.Map<CurrencyDto>(currency);
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

        public CurrencyDto GetById(int id)
        {
            var currency = _currencyRepository.GetById(id);
            return _mapper.Map<CurrencyDto>(currency);
        }

        public void Delete(int id)
        {
            var currency = _currencyRepository.GetById(id)
                ?? throw new BusinessException($"Currency with id '{id}' was not found");
            _currencyRepository.Delete(currency);
        }
    }
}
