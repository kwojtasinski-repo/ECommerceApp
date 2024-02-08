using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRateRepository : ICurrencyRateRepository
    {
        private readonly Context _context; 

        public CurrencyRateRepository(Context context)
        {
            _context = context;
        }

        public CurrencyRate GetById(int id)
        {
            var rate = _context.CurrencyRates.Where(cr => cr.Id == id).FirstOrDefault();
            return rate;
        }

        public List<CurrencyRate> GetAll()
        {
            return _context.CurrencyRates
                           .ToList();
        }

        public void Update(CurrencyRate currencyRate)
        {
            if(currencyRate != null)
            {
                _context.CurrencyRates.Update(currencyRate);
                _context.SaveChanges();
            }
        }

        public void Delete(CurrencyRate currencyRate)
        {
            if(currencyRate != null)
            {
                _context.CurrencyRates.Remove(currencyRate);
                _context.SaveChanges();
            }
        }

        public void Delete(int id)
        {
            var rate = GetById(id);

            if (rate != null)
            {
                _context.CurrencyRates.Remove(rate);
                _context.SaveChanges();
            }
        }

        public int Add(CurrencyRate currencyRate)
        {
            _context.CurrencyRates.Add(currencyRate);
            _context.SaveChanges();
            return currencyRate.Id;
        }

        public CurrencyRate GetRateForDate(int currencyId, DateTime date)
        {
            return _context.CurrencyRates
                           .Where(cr => cr.CurrencyId == currencyId && cr.CurrencyDate == date)
                           .FirstOrDefault();
        }
    }
}
