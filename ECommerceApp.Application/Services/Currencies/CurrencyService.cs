using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Currency;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

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
                throw new BusinessException("Code shouldnt be empty", ErrorCode.Create("currencyCodeEmpty"));
            }

            dto.Code = dto.Code.ToUpper();
            var currency = _mapper.Map<Currency>(dto);
            _currencyRepository.Add(currency);
            return currency.Id;
        }

        public bool Update(CurrencyDto dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(CurrencyDto).Name} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new BusinessException("Code shouldnt be empty", ErrorCode.Create("currencyCodeEmpty"));
            }

            dto.Code = dto.Code.ToUpper();
            var currency = _currencyRepository.GetById(dto.Id);
            if (currency is null)
            {
                return false;
            }

            currency.Code = dto.Code;
            currency.Description = dto.Description;
            _currencyRepository.Update(currency);
            return true;
        }

        public List<CurrencyDto> GetAll()
        {
            List<Currency> currencies = _currencyRepository.GetAll();
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
            var currencies = _currencyRepository.GetAll(pageSize, pageNo, searchString);
            List<CurrencyDto> currencyVms = new ();

            foreach (Currency currency in currencies)
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
                Count = _currencyRepository.GetCountBySearchString(searchString)
            };

            return listCurrency;
        }

        public CurrencyDto GetById(int id)
        {
            var currency = _currencyRepository.GetById(id);
            return _mapper.Map<CurrencyDto>(currency);
        }

        public bool Delete(int id)
        {
            return _currencyRepository.Delete(id);
        }
    }
}
