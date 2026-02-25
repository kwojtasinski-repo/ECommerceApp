using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Repositories
{
    internal sealed class CurrencyRateRepository : ICurrencyRateRepository
    {
        private readonly CurrencyDbContext _context;

        public CurrencyRateRepository(CurrencyDbContext context)
        {
            _context = context;
        }

        public async Task<CurrencyRateId> AddAsync(CurrencyRate currencyRate)
        {
            _context.CurrencyRates.Add(currencyRate);
            await _context.SaveChangesAsync();
            return currencyRate.Id;
        }

        public async Task<CurrencyRate> GetRateForDateAsync(CurrencyId currencyId, DateTime date)
            => await _context.CurrencyRates
                .AsNoTracking()
                .FirstOrDefaultAsync(cr => cr.CurrencyId == currencyId && cr.CurrencyDate == date);
    }
}
