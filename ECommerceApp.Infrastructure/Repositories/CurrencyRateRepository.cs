using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public List<CurrencyRate> GetAll(Expression<Func<CurrencyRate, bool>> expression)
        {
            var rate = _context.CurrencyRates.Where(expression).ToList();
            return rate;
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
    }
}
