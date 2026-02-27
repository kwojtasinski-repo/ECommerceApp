using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Repositories
{
    internal sealed class CurrencyRepository : ICurrencyRepository
    {
        private readonly CurrencyDbContext _context;

        public CurrencyRepository(CurrencyDbContext context)
        {
            _context = context;
        }

        public async Task<CurrencyId> AddAsync(Currency currency)
        {
            _context.Currencies.Add(currency);
            await _context.SaveChangesAsync();
            return currency.Id;
        }

        public async Task<Currency> GetByIdAsync(CurrencyId id)
            => await _context.Currencies.FirstOrDefaultAsync(c => c.Id == id);

        public async Task UpdateAsync(Currency currency)
        {
            _context.Currencies.Update(currency);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(CurrencyId id)
        {
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == id);
            if (currency is null)
                return false;
            _context.Currencies.Remove(currency);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<Currency>> GetAllAsync()
            => await _context.Currencies
                .AsNoTracking()
                .OrderBy(c => EF.Property<string>(c, "Code"))
                .ToListAsync();

        public async Task<List<Currency>> GetAllAsync(int pageSize, int pageNo, string searchString)
            => await _context.Currencies
                .AsNoTracking()
                .Where(c => EF.Property<string>(c, "Code").StartsWith(searchString))
                .OrderBy(c => EF.Property<string>(c, "Code"))
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountBySearchStringAsync(string searchString)
            => await _context.Currencies
                .Where(c => EF.Property<string>(c, "Code").StartsWith(searchString))
                .CountAsync();
    }
}
