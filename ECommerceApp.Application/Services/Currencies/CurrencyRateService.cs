using AutoMapper;
using ECommerceApp.Application.Constants;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.External.POCO;
using ECommerceApp.Application.Utils;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Linq;
using System.Threading;

namespace ECommerceApp.Application.Services.Currencies
{
    public class CurrencyRateService : ICurrencyRateService
    {
        private readonly IMapper _mapper;
        private readonly ICurrencyRateRepository _currencyRateRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly INBPClient _NBPClient;
        private static readonly DateTime archiveDate = new (2002, 1, 2);
        private static readonly int allowedRequests = 15;

        public CurrencyRateService(ICurrencyRateRepository currencyRateRepository, ICurrencyRepository currencyRepository, IMapper mapper, INBPClient nBPClient)
        {
            _mapper = mapper;
            _currencyRateRepository = currencyRateRepository;
            _currencyRepository = currencyRepository;
            _NBPClient = nBPClient;
        }

        public CurrencyRateDto GetRateForDay(int currencyId, DateTime dateTime)
        {
            if (dateTime < archiveDate)
            {
                throw new BusinessException($"There is no rate for {dateTime}");
            }

            var currency = _currencyRepository.GetById(currencyId)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");
            var date = dateTime.Date;

            CurrencyRateDto currencyRateVm;
            if (currencyId == CurrencyConstants.PlnId)
            {
                var currencyRatePLN = TryGetCurrencyRatePLN(currencyId, dateTime);
                currencyRateVm = _mapper.Map<CurrencyRateDto>(currencyRatePLN);
                return currencyRateVm;
            }

            var currencyRate = GetCurrencyRate(currency, date);

            if (currencyRate.Id != 0)
            {
                currencyRateVm = _mapper.Map<CurrencyRateDto>(currencyRate);
                return currencyRateVm;
            }

            _currencyRateRepository.Add(currencyRate);

            currencyRateVm = _mapper.Map<CurrencyRateDto>(currencyRate);
            return currencyRateVm;
        }

        public CurrencyRateDto GetLatestRate(int currencyId)
        {
            var currency = _currencyRepository.GetById(currencyId)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");
            var date = DateTime.Now.Date;

            if (currencyId == CurrencyConstants.PlnId)
            {
                var currencyRatePLN = TryGetCurrencyRatePLN(currencyId, date);
                var currencyRatePLNVm = _mapper.Map<CurrencyRateDto>(currencyRatePLN);
                return currencyRatePLNVm;
            }

            var currencyRate = GetCurrencyRate(currency, date);
            CurrencyRateDto currencyRateVm;
            if (currencyRate.Id != 0)
            {
                currencyRateVm = _mapper.Map<CurrencyRateDto>(currencyRate);
                return currencyRateVm;
            }

            _currencyRateRepository.Add(currencyRate);
            currencyRateVm = _mapper.Map<CurrencyRateDto>(currencyRate);

            return currencyRateVm;
        }

        private CurrencyRate TryGetCurrencyRatePLN(int currencyId, DateTime date)
        {
            var rate = _currencyRateRepository.GetRateForDate(currencyId, date);

            if (rate != null)
            {
                return rate;
            }

            var currencyRatePLN = new CurrencyRate
            {
                CurrencyId = currencyId,
                Rate = new decimal(1.0),
                CurrencyDate = date
            };

            _currencyRateRepository.Add(currencyRatePLN);

            return currencyRatePLN;
        }

        private CurrencyRate GetCurrencyRate(Currency currency, DateTime date)
        {
            var requestCounts = 1;
            ExchangeRate exchangeRate = null;
            date = date.Date;

            while (exchangeRate is null)
            {
                var rate = _currencyRateRepository.GetRateForDate(currency.Id, date);

                if (rate != null)
                {
                    return rate;
                }

                var content = _NBPClient.GetCurrencyRateOnDate(currency.Code, date, CancellationToken.None).Result;
                exchangeRate = NBPResponseUtils.GetResponseContent<ExchangeRate>(content);

                if (exchangeRate is null)
                {
                    date = date.AddDays(-1);
                }

                if (date < archiveDate)
                {
                    throw new BusinessException($"There is no rate for {date}");
                }

                if (requestCounts > allowedRequests)
                {
                    throw new BusinessException($"Check currency code {currency.Code} if is valid");
                }

                requestCounts++;
            }

            var currencyRate = new CurrencyRate
            {
                CurrencyId = currency.Id,
                Rate = exchangeRate.Rates.FirstOrDefault().Mid,
                CurrencyDate = date
            };

            return currencyRate;
        }
    }
}
