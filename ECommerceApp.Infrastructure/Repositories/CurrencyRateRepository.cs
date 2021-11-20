using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Exceptions;
using ECommerceApp.Infrastructure.External.Client;
using ECommerceApp.Infrastructure.External.POCO;
using ECommerceApp.Infrastructure.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRateRepository : GenericRepository<CurrencyRate>, ICurrencyRateRepository
    {
        private readonly INBPClient _NBPClient;
        private readonly ICurrencyRepository _currencyRepository;
        private static DateTime archiveDate = new DateTime(2002, 1, 2);
        private static int allowedRequests = 15;

        public CurrencyRateRepository(Context context, INBPClient client, ICurrencyRepository currencyRepository) : base(context)
        {
            _NBPClient = client;
            _currencyRepository = currencyRepository;
        }

        public override int Add(CurrencyRate entity)
        {
            var type = entity.GetType();
            throw new InfrastructureException($"For entity '{type.Name}' using method Add is not allowed");
        }

        public override Task<int> AddAsync(CurrencyRate entity)
        {
            var type = entity.GetType();
            throw new InfrastructureException($"For entity '{type.Name}' using method AddAsync is not allowed");
        }

        public override List<int> AddRange(List<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method AddRange is not allowed");
        }

        public override Task<List<int>> AddRangeAsync(List<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method AddRangeAsync is not allowed");
        }

        public override void Delete(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Delete is not allowed");
        }

        public override void Delete(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Delete is not allowed");
        }

        public override Task DeleteAsync(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method DeleteAsync is not allowed");
        }

        public override Task DeleteAsync(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method DeleteAsync is not allowed");
        }

        public override CurrencyRate GetById(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method GetById is not allowed");
        }

        public override Task<CurrencyRate> GetByIdAsync(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method GetByIdAsync is not allowed");
        }

        public override void Update(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Update is not allowed");
        }

        public override Task UpdateAsync(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method UpdateAsync is not allowed");
        }

        public override void UpdateRange(IEnumerable<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method UpdateRange is not allowed");
        }

        public decimal GetRateForDay(int currencyId, DateTime dateTime)
        {
            var date = dateTime.Date;
            var currencyRate = TryGetCurrencyRatePLN(currencyId, dateTime);
            if (currencyRate != null)
            {
                return currencyRate.Rate;
            }

            var currency = _currencyRepository.GetAll().Where(c => c.Id == currencyId).FirstOrDefault();

            if (currency is null)
            {
                throw new InfrastructureException($"Currency with id: {currencyId} not found");
            }

            var requestCounts = 1;
            ExchangeRate exchangeRate = null;

            while (exchangeRate is null)
            {
                var rate = _context.CurrencyRates.Where(cr => cr.CurrencyId == currencyId && cr.CurrencyDate == date).FirstOrDefault();

                if (rate != null)
                {
                    return rate.Rate;
                }

                var content = _NBPClient.GetCurrencyRateOnDate(currency.Code, dateTime, CancellationToken.None).Result;
                exchangeRate = NBPResponseUtils.GetResponseContent<ExchangeRate>(content);

                if (exchangeRate is null)
                {
                    date = date.AddDays(-1);
                }

                if (date < archiveDate)
                {
                    throw new InfrastructureException($"There is no rate for {date}");
                }

                if (requestCounts > allowedRequests)
                {
                    throw new InfrastructureException($"Check currency code {currency.Code} if is valid");
                }

                requestCounts++;
            }

            currencyRate = new CurrencyRate
            {
                CurrencyId = currencyId,
                Rate = exchangeRate.Rates.FirstOrDefault().Mid,
                CurrencyDate = date
            };

            _context.CurrencyRates.Add(currencyRate);
            _context.SaveChanges();

            return currencyRate.Rate;
        }

        public decimal GetLatestRate(int currencyId)
        {
            var date = DateTime.Now.Date;
            var currencyRate = _context.CurrencyRates.Where(cr => cr.CurrencyId == currencyId && cr.CurrencyDate == date).FirstOrDefault();

            if (currencyRate != null)
            {
                return currencyRate.Rate;
            }

            var currency = _currencyRepository.GetAll().Where(c => c.Id == currencyId).FirstOrDefault();

            if (currency is null)
            {
                throw new InfrastructureException($"Currency with id: {currencyId} not found");
            }

            if (currencyId == 1) 
            {
                var rate = TryGetCurrencyRatePLN(currencyId, date);
                return rate.Rate;
            }

            var rateString = _NBPClient.GetCurrency(currency.Code, CancellationToken.None).Result;
            var exchangeRate = NBPResponseUtils.GetResponseContent<ExchangeRate>(rateString);
            currencyRate = new CurrencyRate { Id = 0, CurrencyId = currencyId, CurrencyDate = exchangeRate.Rates[0].EffectiveDate, Rate = exchangeRate.Rates[0].Mid };
            _context.Add(currencyRate);

            return currencyRate.Rate;
        }

        private CurrencyRate TryGetCurrencyRatePLN(int currencyId, DateTime date)
        {
            var rate = _context.CurrencyRates.Where(cr => cr.CurrencyId == currencyId && cr.CurrencyDate == date).FirstOrDefault();

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
            _context.CurrencyRates.Add(currencyRatePLN);
            _context.SaveChanges();

            return currencyRatePLN;
        }
    }
}
