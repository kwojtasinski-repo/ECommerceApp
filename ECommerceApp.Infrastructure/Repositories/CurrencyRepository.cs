using ECommerceApp.Domain.Model;
using ECommerceApp.Domain.Interface;
using System.Linq;
using ECommerceApp.Infrastructure.Database;
using System.Collections.Generic;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRepository : ICurrencyRepository
    {
        private readonly IGenericRepository<Currency> _currencyRepository;
        private readonly Context _context;

        public CurrencyRepository(Context context, IGenericRepository<Currency> currencyRepository)
        {
            _context = context;
            _currencyRepository = currencyRepository;
        }

        public int Add(Currency currency)
        {
            return _currencyRepository.Add(currency);
        }

        public bool Delete(Currency currency)
        {
            return _currencyRepository.Delete(currency);
        }

        public List<Currency> GetAll()
        {
            return _currencyRepository.GetAll().ToList();
        }

        public List<Currency> GetAll(int pageSize, int pageNo, string searchString)
        {
            return _context.Currencies
                           .Where(c => c.Code.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize).ToList();
        }

        public Currency GetById(int id)
        {
            return _currencyRepository.GetById(id);
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Currencies
                           .Where(c => c.Code.StartsWith(searchString))
                           .Count();
        }

        public void Update(Currency currency)
        {
            _currencyRepository.Update(currency);
        }
    }
}
