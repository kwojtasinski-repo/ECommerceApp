using ECommerceApp.Application.Constants;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.External.POCO;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using ECommerceApp.Application.Utils;
using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal sealed class CurrencyRateService : ICurrencyRateService
    {
        private readonly ICurrencyRateRepository _currencyRateRepo;
        private readonly ICurrencyRepository _currencyRepo;
        private readonly INBPClient _nbpClient;
        private readonly IMemoryCache _cache;
        private readonly CacheOptions _cacheOptions;
        private static readonly DateTime ArchiveDate = new(2002, 1, 2);
        private static readonly int AllowedRequests = 15;

        public CurrencyRateService(
            ICurrencyRateRepository currencyRateRepo,
            ICurrencyRepository currencyRepo,
            INBPClient nbpClient,
            IMemoryCache cache,
            IOptions<CacheOptions> cacheOptions)
        {
            _currencyRateRepo = currencyRateRepo;
            _currencyRepo = currencyRepo;
            _nbpClient = nbpClient;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        public async Task<CurrencyRateVm> GetRateForDayAsync(int currencyId, DateTime dateTime)
        {
            if (dateTime < ArchiveDate)
                throw new BusinessException($"There is no rate for {dateTime}");

            var date = dateTime.Date;
            var cacheKey = $"CurrencyRate:{currencyId}:{date:yyyy-MM-dd}";

            // Only use the cache for historical (immutable) dates.
            if (date < DateTime.UtcNow.Date && _cache.TryGetValue(cacheKey, out CurrencyRateVm historicalCached))
                return historicalCached;

            var id = new CurrencyId(currencyId);
            var currency = await _currencyRepo.GetByIdAsync(id)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");

            CurrencyRateVm vm;

            if (id == Currency.PlnId)
            {
                var plnRate = await GetOrCreatePlnRateAsync(id, date);
                vm = CurrencyRateVm.FromDomain(plnRate);
            }
            else
            {
                var currencyRate = await GetOrFetchRateAsync(currency, id, date);
                vm = CurrencyRateVm.FromDomain(currencyRate);
            }

            // Cache historical dates (rates never change retroactively).
            if (date < DateTime.UtcNow.Date)
                _cache.Set(cacheKey, vm, _cacheOptions.CurrencyRateHistoricalTtl);

            return vm;
        }

        public async Task<CurrencyRateVm> GetLatestRateAsync(int currencyId)
        {
            var cacheKey = $"CurrencyRate:Latest:{currencyId}";
            if (_cache.TryGetValue(cacheKey, out CurrencyRateVm latestCached))
                return latestCached;

            var id = new CurrencyId(currencyId);
            var currency = await _currencyRepo.GetByIdAsync(id)
                ?? throw new BusinessException($"Currency with id: {currencyId} not found");

            var date = DateTime.Now.Date;
            CurrencyRateVm vm;

            if (id == Currency.PlnId)
            {
                var plnRate = await GetOrCreatePlnRateAsync(id, date);
                vm = CurrencyRateVm.FromDomain(plnRate);
            }
            else
            {
                var currencyRate = await GetOrFetchRateAsync(currency, id, date);
                vm = CurrencyRateVm.FromDomain(currencyRate);
            }

            _cache.Set(cacheKey, vm, _cacheOptions.CurrencyRateLatestTtl);
            return vm;
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

        private async Task<CurrencyRate> GetOrFetchRateAsync(Currency currency, CurrencyId currencyId, DateTime date)
        {
            var requestCounts = 1;
            ExchangeRate exchangeRate = null;
            var searchDate = date;

            while (exchangeRate is null)
            {
                var existingRate = await _currencyRateRepo.GetRateForDateAsync(currencyId, searchDate);
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

            var currencyRate = CurrencyRate.Create(currencyId, exchangeRate.Rates.FirstOrDefault().Mid, searchDate);
            await _currencyRateRepo.AddAsync(currencyRate);
            return currencyRate;
        }

        public async Task<int> SyncAllRatesAsync(CancellationToken ct = default)
        {
            var content = await _nbpClient.GetCurrencyTable(ct);
            var tables = NBPResponseUtils.GetResponseContent<List<ExchangeRateTable>>(content);
            if (tables is null || tables.Count == 0)
                return 0;

            var ratesByCode = tables[0].Rates
                .ToDictionary(r => r.Code.ToUpperInvariant(), r => r.Mid);

            var currencies = await _currencyRepo.GetAllAsync();
            var date = DateTime.UtcNow.Date;
            var synced = 0;

            foreach (var currency in currencies)
            {
                if (currency.Id == Currency.PlnId)
                    continue;

                if (!ratesByCode.TryGetValue(currency.Code.Value.ToUpperInvariant(), out var mid))
                {
                    continue;
                }

                var existing = await _currencyRateRepo.GetRateForDateAsync(currency.Id, date);
                if (existing is not null)
                    continue;

                var currencyRate = CurrencyRate.Create(currency.Id, mid, date);
                await _currencyRateRepo.AddAsync(currencyRate);
                synced++;
            }

            return synced;
        }
    }
}
