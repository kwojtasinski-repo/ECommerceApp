using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.External.POCO;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using ECommerceApp.Application.Utils;
using ECommerceApp.Domain.Supporting.Currencies;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal sealed class CurrencyRateService : ICurrencyRateService
    {
        private readonly IMapper _mapper;
        private readonly ICurrencyRateRepository _currencyRateRepo;
        private readonly ICurrencyRepository _currencyRepo;
        private readonly INBPClient _nbpClient;
        private static readonly DateTime ArchiveDate = new(2002, 1, 2);
        private static readonly int AllowedRequests = 15;

        public CurrencyRateService(
            ICurrencyRateRepository currencyRateRepo,
            ICurrencyRepository currencyRepo,
            IMapper mapper,
            INBPClient nbpClient)
        {
            _currencyRateRepo = currencyRateRepo;
            _currencyRepo = currencyRepo;
            _mapper = mapper;
            _nbpClient = nbpClient;
        }

        public async Task<CurrencyRateVm> GetRateForDayAsync(int currencyId, DateTime dateTime)
        {
            if (dateTime < ArchiveDate)
                throw new BusinessException($"There is no rate for {dateTime}");

            var id = new CurrencyId(currencyId);
            var currency = await _currencyRepo.GetByIdAsync(id)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");

            var date = dateTime.Date;

            if (id == Currency.PlnId)
            {
                var plnRate = await GetOrCreatePlnRateAsync(id, date);
                return _mapper.Map<CurrencyRateVm>(plnRate);
            }

            var currencyRate = await GetOrFetchRateAsync(currency, date);
            return _mapper.Map<CurrencyRateVm>(currencyRate);
        }

        public async Task<CurrencyRateVm> GetLatestRateAsync(int currencyId)
        {
            var id = new CurrencyId(currencyId);
            var currency = await _currencyRepo.GetByIdAsync(id)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");

            var date = DateTime.Now.Date;

            if (id == Currency.PlnId)
            {
                var plnRate = await GetOrCreatePlnRateAsync(id, date);
                return _mapper.Map<CurrencyRateVm>(plnRate);
            }

            var currencyRate = await GetOrFetchRateAsync(currency, date);
            return _mapper.Map<CurrencyRateVm>(currencyRate);
        }

        private async Task<CurrencyRate> GetOrCreatePlnRateAsync(CurrencyId currencyId, DateTime date)
        {
            var rate = await _currencyRateRepo.GetRateForDateAsync(currencyId, date);
            if (rate != null)
                return rate;

            var plnRate = CurrencyRate.Create(currencyId, 1.0m, date);
            await _currencyRateRepo.AddAsync(plnRate);
            return plnRate;
        }

        private async Task<CurrencyRate> GetOrFetchRateAsync(Currency currency, DateTime date)
        {
            var requestCounts = 1;
            ExchangeRate exchangeRate = null;
            var searchDate = date;

            while (exchangeRate is null)
            {
                var existingRate = await _currencyRateRepo.GetRateForDateAsync(currency.Id, searchDate);
                if (existingRate != null)
                    return existingRate;

                var content = await _nbpClient.GetCurrencyRateOnDate(currency.Code.Value, searchDate, CancellationToken.None);
                exchangeRate = NBPResponseUtils.GetResponseContent<ExchangeRate>(content);

                if (exchangeRate is null)
                    searchDate = searchDate.AddDays(-1);

                if (searchDate < ArchiveDate)
                    throw new BusinessException($"There is no rate for {searchDate}");

                if (requestCounts > AllowedRequests)
                    throw new BusinessException($"Check currency code {currency.Code.Value} if is valid");

                requestCounts++;
            }

            var currencyRate = CurrencyRate.Create(currency.Id, exchangeRate.Rates.FirstOrDefault().Mid, searchDate);
            await _currencyRateRepo.AddAsync(currencyRate);
            return currencyRate;
        }
    }
}
