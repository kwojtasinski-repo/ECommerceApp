using ECommerceApp.Domain.Model;
using ECommerceApp.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using ECommerceApp.Infrastructure.Database;

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

        public List<Currency> GetAll(Expression<Func<Currency, bool>> expression)
        {
            var currencies = _context.Currencies.Where(expression).ToList();
            return currencies;
        }

        public IQueryable<Currency> GetAll()
        {
            return _currencyRepository.GetAll();
        }

        public Currency GetById(int id)
        {
            return _currencyRepository.GetById(id);
        }

        public void Update(Currency currency)
        {
            _currencyRepository.Update(currency);
        }
    }
}
